// 仕様書: Unity/docs/.sdd/matchmaking/01-matchmaking-flow.md
// MatchMakingCanvas 直下の3パネル（WriteNameModal / WaitingPanel / MatchingPanel）を
// MatchmakingViewState.Panel に従って切り替える。どのパネルを出すかの判定は値オブジェクト側に
// あり、この MonoBehaviour は表示の反映だけを行う（value-objects/README.md §1）。
//
// 表示名の送信（REQ-01・Proto v0.4.0）と PaticipantsList（REQ-03・Proto v0.5.0）は
// どちらも上流の契約更新により実装済み。表示名は GameBootstrapper.DecideDisplayName から
// MatchClientController.BeginPlay へ渡り、接続確立直後の MatchmakingJoin に乗る。

using System;
using System.Collections.Generic;
using Takoda99.Client.Net;
using Takoda99.Client.State;
using Takoda99.Sound;
using Takoda99.View.ValueObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>マッチング画面。ClientState と Dispatcher の通知から MatchmakingViewState を導出する。</summary>
    public sealed class MatchmakingScreenView : MonoBehaviour
    {
        [Header("3パネル（MatchMakingCanvas の直下）")]
        [SerializeField] private GameObject writeNameModal;
        [SerializeField] private GameObject waitingPanel;
        [SerializeField] private GameObject matchingPanel;

        [Header("WriteNameModal")]
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button decideButton;

        [Header("MatchingPanel")]
        [SerializeField] private TextMeshProUGUI timerText;             // Timer/Text (TMP)
        [SerializeField] private TextMeshProUGUI participantsNumText;   // PaticipantsNumPanel/Text (TMP)
        [SerializeField] private RectTransform participantsContainer;   // PaticipantsList/Paticipants
        [SerializeField] private PaticipantView participantPrefab;      // Prefabs/MatchMakingCanvas/Paticipant
        [SerializeField] private GameObject matchingComplete;           // MatchingComplete（カウントダウン後〜MatchStart まで）

        [Tooltip("残り秒数の文面。{0} が秒数に置き換わる。")]
        [SerializeField] private string timerFormat = "のこり{0}秒";

        [Tooltip("参加者一覧のマス目サイズを決める基準人数。実際の人数がこれより少なくても、" +
                 "マスは常にこの人数ぶんの大きさで敷き詰める（自分1人だけのときに巨大な1枠になるのを防ぐ）。")]
        [SerializeField] private int participantLayoutCapacity = 99;

        /// <summary>入力欄の上限。UX のための制限であり、サーバー正規化（24文字）の代替ではない（02-display-name.md §4）。</summary>
        public const int DisplayNameInputLimit = 6;

        private bool hasReceivedStatus;
        private bool nameDecided;
        private IDispatcher dispatcher;
        private IDisposable subscription;
        private Bootstrap.GameBootstrapper bootstrap;
        private readonly List<PaticipantView> participantViews = new();

        // カウントダウンの締切（ローカル単調時刻ms）。サーバーは尽きた瞬間に 0 を送り直さないため、
        // 「尽きた」の判定だけはローカルで持つ（表示上の目安であり、成立判定ではない）。
        private long? countdownDeadlineMs;
        private int? lastCountdownMs;
        private bool hasCountdownObservation;

        /// <summary>直近に描いたパネル。Update から MatchingComplete を出し直す判断に使う。</summary>
        private MatchmakingPanel currentPanel;

        /// <summary>マッチング成立のSEを鳴らしたか。ApplyCountdown は毎フレーム通るため、1試合1回に絞る。</summary>
        private bool hasPlayedCompleteSe;

        private void OnEnable()
        {
            bootstrap = Bootstrap.GameBootstrapper.Instance;
            if (bootstrap == null)
            {
                Debug.LogError($"{nameof(MatchmakingScreenView)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が見つかりません。Boot シーンから再生してください。", this);
                return;
            }

            if (nameInputField != null)
            {
                nameInputField.characterLimit = DisplayNameInputLimit;

                // WebGL では IME が効かないため、ブラウザの input を重ねる WebGLInput を実行時に付ける。
                WebGLNameInputImeBridge.Attach(nameInputField);
            }

            if (decideButton != null)
            {
                decideButton.onClick.AddListener(OnDecideClicked);
            }

            Bind(bootstrap.Store, bootstrap.Dispatcher);
        }

        private void OnDisable()
        {
            if (decideButton != null)
            {
                decideButton.onClick.RemoveListener(OnDecideClicked);
            }

            subscription?.Dispose();
            subscription = null;

            if (dispatcher != null)
            {
                dispatcher.OnActionApplied -= HandleActionApplied;
                dispatcher = null;
            }
        }

        /// <summary>IStore / IDispatcher を注入する。通常は OnEnable が自動で呼ぶ。</summary>
        public void Bind(IStore store, IDispatcher boundDispatcher)
        {
            subscription?.Dispose();

            dispatcher = boundDispatcher;
            dispatcher.OnActionApplied += HandleActionApplied;
            subscription = store.Subscribe(Render);
            Render(store.State);
        }

        /// <summary>
        /// Decide 押下。**ここで初めて接続する。** 接続してから名前を入力させると、サーバーの
        /// 3秒の待ち受けを超えて表示名が失われる（02-display-name.md §5 ★）。
        /// </summary>
        private void OnDecideClicked()
        {
            if (nameDecided || bootstrap == null)
            {
                return;
            }

            nameDecided = true;
            SoundPlayer.Play(SoundId.ButtonTap);
            bootstrap.DecideDisplayName(nameInputField != null ? nameInputField.text : string.Empty);
        }

        private void HandleActionApplied(IAction action)
        {
            if (action is MatchmakingStatusAction)
            {
                hasReceivedStatus = true;
            }
        }

        private void Render(ClientState state)
        {
            var matchStarted = state.Phase == ClientPhase.InMatch
                || state.Phase == ClientPhase.Spectating
                || state.Phase == ClientPhase.Result;
            var connected = state.Phase == ClientPhase.Matchmaking || matchStarted;

            var participants = new (string StoreId, string DisplayName)[state.MatchmakingParticipants.Count];
            for (var i = 0; i < participants.Length; i++)
            {
                var p = state.MatchmakingParticipants[i];
                participants[i] = (p.StoreId, p.DisplayName);
            }

            var view = MatchmakingViewState.From(
                state.Connection == ConnectionState.Failed,
                nameDecided,
                connected,
                matchStarted,
                hasReceivedStatus,
                state.WaitingCount,
                state.MinPlayers,
                state.CountdownMs,
                state.SelfStoreId,
                participants);

            Apply(view);
        }

        private void Apply(MatchmakingViewState view)
        {
            currentPanel = view.Panel;

            SetActive(writeNameModal, view.Panel == MatchmakingPanel.WriteNameModal);
            SetActive(waitingPanel, view.Panel == MatchmakingPanel.WaitingPanel);
            SetActive(matchingPanel, view.Panel == MatchmakingPanel.MatchingPanel);

            if (participantsNumText != null)
            {
                participantsNumText.text = view.WaitingCount.ToString();
            }

            TrackCountdown(view.CountdownMs);
            ApplyCountdown();
            ApplyParticipants(view.Participants);
        }

        /// <summary>
        /// カウントダウンの締切をサーバー値に合わせ直す。値が変わったときだけ再同期し、
        /// 尽きたあとの経過はローカルで進める。
        /// </summary>
        private void TrackCountdown(int? countdownMs)
        {
            if (hasCountdownObservation && countdownMs == lastCountdownMs)
            {
                return;
            }

            hasCountdownObservation = true;
            lastCountdownMs = countdownMs;

            if (countdownMs.HasValue)
            {
                countdownDeadlineMs = NowMs() + countdownMs.Value;
                return;
            }

            // まだ時間が残っているうちに countdownMs が消えたのなら中断（minPlayers 割れ・§5.2）。
            // 尽きたあとの欠落は完了なので、締切はそのまま残す。
            if (countdownDeadlineMs.HasValue && countdownDeadlineMs.Value - NowMs() > 0L)
            {
                countdownDeadlineMs = null;
            }
        }

        private void Update()
        {
            // カウントダウンが尽きる瞬間は MatchmakingStatus の受信と一致しない
            // （サーバーは尽きた時に送り直さない）ため、毎フレーム見に行く。
            if (currentPanel == MatchmakingPanel.MatchingPanel)
            {
                ApplyCountdown();
            }
        }

        private void ApplyCountdown()
        {
            var remaining = countdownDeadlineMs.HasValue
                ? (long?)(countdownDeadlineMs.Value - NowMs())
                : null;

            var countdown = MatchmakingCountdownState.From(lastCountdownMs, remaining);

            if (timerText != null)
            {
                // countdownMs はカウントダウン中しか届かない。欠落を 0 と表示すると
                // 「あと0秒」のまま始まらない画面になる（§3 / §5.2）。
                timerText.text = countdown.IsCountingDown || countdown.IsComplete
                    ? string.Format(timerFormat, countdown.RemainingSeconds)
                    : string.Empty;
            }

            // 締切を過ぎてから MatchStart が届くまでの数秒だけ出す。
            // MatchStart で Panel が None になるため、ここで畳む処理は要らない。
            SetActive(matchingComplete, countdown.IsComplete);

            // 成立の合図は MatchingComplete が出た瞬間の1回だけ。
            // このメソッドは毎フレーム通るので、フラグで押さえないと鳴りっぱなしになる。
            if (countdown.IsComplete && !hasPlayedCompleteSe)
            {
                hasPlayedCompleteSe = true;
                SoundPlayer.Play(SoundId.MatchmakingComplete);
            }
        }

        /// <summary>ローカル単調時刻(ms)。カウントダウンの残りを引くためだけに使う。</summary>
        private static long NowMs()
        {
            return (long)(Time.realtimeSinceStartupAsDouble * 1000d);
        }

        /// <summary>Paticipant プレハブの幅:高さ比（45:50）。Inspector 側の実サイズが変わったらここも合わせる。</summary>
        private const float ParticipantAspect = 45f / 50f;

        /// <summary>枠どうしの隙間ぶん、セルに対して縮める比率（1マス丸ごとだと詰まって見えるため）。</summary>
        private const float ParticipantFillRatio = 0.9f;

        /// <summary>1段おきに横へずらす量（セル幅に対する比率）。レンガ状にして単調な格子を崩す。</summary>
        private const float ParticipantRowStaggerRatio = 0.5f;

        /// <summary>「ちょっとずらす」ための微小なジッター量（セルサイズに対する比率）。</summary>
        private const float ParticipantJitterRatio = 0.12f;

        private bool participantsLayoutGroupDisabled;

        /// <summary>参加者一覧を表示ぶんだけ生成し、増減に合わせてプレハブを足し引きする。</summary>
        private void ApplyParticipants(IReadOnlyList<MatchmakingParticipantView> participants)
        {
            if (participantsContainer == null || participantPrefab == null)
            {
                return;
            }

            // レイアウトはこちらで自前計算する（千鳥配置・ジッターは GridLayoutGroup では組めない）。
            // シーン側に GridLayoutGroup が残っていても、初回だけ無効化して competing させない。
            if (!participantsLayoutGroupDisabled)
            {
                var legacyLayout = participantsContainer.GetComponent<GridLayoutGroup>();
                if (legacyLayout != null)
                {
                    legacyLayout.enabled = false;
                }

                participantsLayoutGroupDisabled = true;
            }

            while (participantViews.Count < participants.Count)
            {
                var instance = Instantiate(participantPrefab, participantsContainer);
                participantViews.Add(instance);
            }

            // マスの大きさは常に participantLayoutCapacity 人ぶんで計算する。実人数で計算すると、
            // 人数が少ないうちはマスが巨大になり（自分1人だけで画面いっぱい、等）、人数が増えるたびに
            // 縮んでしまう。99人そろったときの見た目を最初から出す。
            var layoutCapacity = Mathf.Max(participants.Count, participantLayoutCapacity);
            var layout = ComputeParticipantLayout(layoutCapacity, participantsContainer.rect.size);

            for (var i = 0; i < participantViews.Count; i++)
            {
                var active = i < participants.Count;
                var view = participantViews[i];
                view.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                view.Apply(participants[i].DisplayName, participants[i].IsSelf);
                PlaceParticipant((RectTransform)view.transform, i, layout);
            }
        }

        /// <summary>マス目の配置。列数・行数・1マスのサイズ・原点（コンテナ中心からの左上オフセット）を持つ。</summary>
        private readonly struct ParticipantGridLayout
        {
            public ParticipantGridLayout(int columns, Vector2 cellSize, Vector2 origin, float rowStagger)
            {
                Columns = columns;
                CellSize = cellSize;
                Origin = origin;
                RowStagger = rowStagger;
            }

            public int Columns { get; }
            public Vector2 CellSize { get; }
            public Vector2 Origin { get; }
            public float RowStagger { get; }
        }

        /// <summary>
        /// 人数とコンテナのサイズから、なるべく大きなマスで敷き詰められる列数を探す。
        /// 1段おきの千鳥ずらし（半マス）ぶんの余白も、探索の時点で確保しておく。
        /// </summary>
        private static ParticipantGridLayout ComputeParticipantLayout(int count, Vector2 containerSize)
        {
            if (count <= 0 || containerSize.x <= 0f || containerSize.y <= 0f)
            {
                return new ParticipantGridLayout(1, Vector2.one, Vector2.zero, 0f);
            }

            var bestColumns = 1;
            var bestCellWidth = 0f;

            for (var columns = 1; columns <= count; columns++)
            {
                var rows = Mathf.CeilToInt(count / (float)columns);
                var widthUnits = columns + ParticipantRowStaggerRatio; // 千鳥ずらしぶんの余白を含めた実効列数
                var cellWidthByWidth = containerSize.x / widthUnits;
                var cellWidthByHeight = (containerSize.y / rows) * ParticipantAspect;
                var cellWidth = Mathf.Min(cellWidthByWidth, cellWidthByHeight);

                if (cellWidth > bestCellWidth)
                {
                    bestCellWidth = cellWidth;
                    bestColumns = columns;
                }
            }

            var finalRows = Mathf.CeilToInt(count / (float)bestColumns);
            var cellW = bestCellWidth;
            var cellH = cellW / ParticipantAspect;
            var gridWidth = cellW * (bestColumns + ParticipantRowStaggerRatio);
            var gridHeight = cellH * finalRows;
            var origin = new Vector2(-gridWidth / 2f, gridHeight / 2f);

            return new ParticipantGridLayout(bestColumns, new Vector2(cellW, cellH), origin, cellW * ParticipantRowStaggerRatio);
        }

        /// <summary>index 番目の参加者を、格子上の位置＋段ずらし＋微小ジッターで配置する。</summary>
        private static void PlaceParticipant(RectTransform rect, int index, ParticipantGridLayout layout)
        {
            var row = index / layout.Columns;
            var column = index % layout.Columns;
            var isStaggeredRow = row % 2 == 1;

            var cellW = layout.CellSize.x;
            var cellH = layout.CellSize.y;

            var x = layout.Origin.x + (column * cellW) + (isStaggeredRow ? layout.RowStagger : 0f) + (cellW / 2f);
            var y = layout.Origin.y - (row * cellH) - (cellH / 2f);

            // 「ちょっとずらす」：格子の機械的な整列感を崩す、安定した（毎フレーム変わらない）微小ジッター。
            var jitterRandom = new System.Random(index * 7919 + 13);
            var jitterX = ((float)jitterRandom.NextDouble() - 0.5f) * cellW * ParticipantJitterRatio;
            var jitterY = ((float)jitterRandom.NextDouble() - 0.5f) * cellH * ParticipantJitterRatio;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(cellW * ParticipantFillRatio, cellH * ParticipantFillRatio);
            rect.anchoredPosition = new Vector2(x + jitterX, y + jitterY);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}

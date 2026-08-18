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

        /// <summary>参加者一覧を表示ぶんだけ生成し、増減に合わせてプレハブを足し引きする。</summary>
        private void ApplyParticipants(IReadOnlyList<MatchmakingParticipantView> participants)
        {
            if (participantsContainer == null || participantPrefab == null)
            {
                return;
            }

            while (participantViews.Count < participants.Count)
            {
                var instance = Instantiate(participantPrefab, participantsContainer);
                participantViews.Add(instance);
            }

            for (var i = 0; i < participantViews.Count; i++)
            {
                var active = i < participants.Count;
                participantViews[i].gameObject.SetActive(active);
                if (active)
                {
                    participantViews[i].Apply(participants[i].DisplayName, participants[i].IsSelf);
                }
            }
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

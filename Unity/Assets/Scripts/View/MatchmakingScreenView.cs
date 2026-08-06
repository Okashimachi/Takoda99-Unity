// 仕様書: Unity/docs/.sdd/matchmaking/01-matchmaking-flow.md
// MatchMakingCanvas 直下の3パネル（WriteNameModal / WaitingPanel / MatchingPanel）を
// MatchmakingViewState.Panel に従って切り替える。どのパネルを出すかの判定は値オブジェクト側に
// あり、この MonoBehaviour は表示の反映だけを行う（value-objects/README.md §1）。
//
// ⚠ 未実装（上流待ち。実装するとルール違反になるため意図的に空けてある）
//   1. 表示名の送信 … Proto の C# ミラーに MatchmakingJoin.displayName が無い
//      （02-display-name.md §2 / docs/server-sync REQ-01）。名前は保持のみ行う。
//   2. PaticipantsList（99人の名前一覧）… マッチング中に参加者名を配る契約が存在しない
//      （MatchmakingStatus は waitingCount / minPlayers / countdownMs のみ）。REQ-03 の回答待ち。

using System;
using Takoda99.Client.Net;
using Takoda99.Client.State;
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

        /// <summary>入力欄の上限。UX のための制限であり、サーバー正規化（24文字）の代替ではない（02-display-name.md §4）。</summary>
        public const int DisplayNameInputLimit = 6;

        private bool hasReceivedStatus;
        private bool nameDecided;
        private IDispatcher dispatcher;
        private IDisposable subscription;
        private Bootstrap.GameBootstrapper bootstrap;

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

            var view = MatchmakingViewState.From(
                state.Connection == ConnectionState.Failed,
                nameDecided,
                connected,
                matchStarted,
                hasReceivedStatus,
                state.WaitingCount,
                state.MinPlayers,
                state.CountdownMs);

            Apply(view);
        }

        private void Apply(MatchmakingViewState view)
        {
            SetActive(writeNameModal, view.Panel == MatchmakingPanel.WriteNameModal);
            SetActive(waitingPanel, view.Panel == MatchmakingPanel.WaitingPanel);
            SetActive(matchingPanel, view.Panel == MatchmakingPanel.MatchingPanel);

            if (participantsNumText != null)
            {
                participantsNumText.text = view.WaitingCount.ToString();
            }

            if (timerText != null)
            {
                // countdownMs はカウントダウン中しか届かない。欠落を 0 と表示すると
                // 「あと0秒」のまま始まらない画面になる（§3 / §5.2）。
                timerText.text = view.CountdownMs.HasValue
                    ? Mathf.CeilToInt(view.CountdownMs.Value / 1000f).ToString()
                    : string.Empty;
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

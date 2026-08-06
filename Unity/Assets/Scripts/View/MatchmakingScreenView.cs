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

        /// <summary>入力欄の上限。UX のための制限であり、サーバー正規化（24文字）の代替ではない（02-display-name.md §4）。</summary>
        public const int DisplayNameInputLimit = 6;

        private bool hasReceivedStatus;
        private bool nameDecided;
        private IDispatcher dispatcher;
        private IDisposable subscription;
        private Bootstrap.GameBootstrapper bootstrap;
        private readonly List<PaticipantView> participantViews = new();

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

            ApplyParticipants(view.Participants);
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

// 仕様書: Unity/docs/.sdd/match-view/01-renderer.md
// IRenderer の Unity 実体。MatchClientController からの離散イベントと IStore の連続的な状態変化を
// 既存の下位View（MainStoreView / SubStoreBoardView / PatienceTimer）へ振り分ける。

using System;
using System.Linq;
using Takoda99.Client.Lifecycle;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;
using Takoda99.Timer;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary><see cref="IRenderer"/> の Unity 実体（01-renderer.md）。</summary>
    public sealed class Renderer : MonoBehaviour, IRenderer
    {
        [SerializeField] private MainStoreView mainStore;
        [SerializeField] private SubStoreBoardView subStoreBoard;
        [SerializeField] private PatienceTimer patienceTimer;

        private IStore store;
        private ITypingJudge typingJudge;
        private IDisposable subscription;
        private string servingCustomerId;

        /// <summary>IStore / ITypingJudge を注入する（01-renderer.md §3）。通常は OnEnable が自動で呼ぶ。</summary>
        public void Bind(IStore boundStore, ITypingJudge boundTypingJudge)
        {
            subscription?.Dispose();

            store = boundStore;
            typingJudge = boundTypingJudge;
            subscription = store.Subscribe(HandleStateChanged);
            HandleStateChanged(store.State);
        }

        private void OnEnable()
        {
            var bootstrap = Bootstrap.GameBootstrapper.Instance;
            if (bootstrap == null)
            {
                Debug.LogError($"{nameof(Renderer)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が見つかりません。Bootstrap シーンが先にロードされているか確認してください。", this);
                return;
            }

            Bind(bootstrap.Store, bootstrap.TypingJudge);
            bootstrap.AttachRenderer(this);
        }

        private void OnDisable()
        {
            Bootstrap.GameBootstrapper.Instance?.DetachRenderer(this);
            subscription?.Dispose();
        }

        private void HandleStateChanged(ClientState state)
        {
            if (mainStore != null)
            {
                mainStore.SetCreditLife(state.CreditLife);
                mainStore.SetEvaluation(state.Normalized, state.Alive);
                ApplyWord(state);
            }

            if (subStoreBoard != null)
            {
                foreach (var summary in state.Stores.Where(s => s.StoreId != state.SelfStoreId))
                {
                    subStoreBoard.SetSummary(summary.StoreId, summary.CreditLife, summary.Alive);
                    if (summary.FinalRank.HasValue)
                    {
                        subStoreBoard.SetRank(summary.StoreId, summary.FinalRank.Value);
                    }
                }
            }

            ApplyServingCustomer(state);
        }

        private void ApplyWord(ClientState state)
        {
            if (state.CurrentOrder is null || typingJudge is null)
            {
                mainStore.SetWord(string.Empty, string.Empty);
                return;
            }

            var view = typingJudge.CurrentView;
            mainStore.SetWord(view.CurrentWord, string.Empty);
            mainStore.SetTypedProgress(view.TypedKanaLength, 0);
        }

        private void ApplyServingCustomer(ClientState state)
        {
            if (patienceTimer == null)
            {
                return;
            }

            var front = state.Queue.Count > 0 ? state.Queue[0] : null;
            var frontId = front?.View.CustomerId;

            if (frontId == servingCustomerId)
            {
                return;
            }

            servingCustomerId = frontId;

            if (front is null)
            {
                patienceTimer.Stop();
                return;
            }

            patienceTimer.Begin(front.ArrivedAtLocalMs, front.View.PatienceMaxMs);
        }

        // ── IRenderer ────────────────────────────────────────────────

        public void OnCustomerArrived(CustomerView customer)
        {
            // 対応中客の検知は HandleStateChanged 側で行う（01-renderer.md §4.2）。
        }

        public void OnCustomerLeft(string customerId, LeaveReason reason)
        {
            if (patienceTimer != null && customerId == servingCustomerId)
            {
                patienceTimer.Stop();
            }
        }

        public void OnKeyFeedback(KeyResult result)
        {
        }

        public void OnOrderServed(string customerId)
        {
        }

        public void OnPhaseChanged(Phase phase)
        {
        }

        public void OnForcedEliminationWarning(int untilTick, double thresholdPct)
        {
        }

        public void OnStoreEliminated(string storeId, EliminationReason reason, int finalRank)
        {
        }

        public void OnMatchEnd(int finalRank, MatchStats stats)
        {
        }

        public void OnLifecycleChanged(ClientPhase from, ClientPhase to)
        {
        }

        public void OnConnectionTrouble(string kind)
        {
            Debug.LogWarning($"{nameof(Renderer)}: connection trouble ({kind})", this);
        }
    }
}

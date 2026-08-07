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
        [SerializeField] private EliminationResultView resultView;
        [SerializeField] private Customers.CustomerQueueView customerQueue;

        private IStore store;
        private ITypingJudge typingJudge;
        private IDisposable subscription;
        private string servingCustomerId;

        /// <summary>自店が脱落済みか。以降は観戦なので行列を描かない。</summary>
        private bool selfEliminated;

        /// <summary>IStore / ITypingJudge を注入する（01-renderer.md §3）。通常は OnEnable が自動で呼ぶ。</summary>
        public void Bind(IStore boundStore, ITypingJudge boundTypingJudge)
        {
            subscription?.Dispose();

            store = boundStore;
            typingJudge = boundTypingJudge;
            selfEliminated = false;
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

            // 行列の描画。ここを呼ばないと、サーバー由来の客が state.Queue に溜まるだけで
            // 画面に一切出ない（この結線漏れが「客がテストドライバ由来になっていた」原因）。
            // 自店が脱落した後は観戦なので、state.Queue に何が残っていても行列は描かない。
            if (customerQueue != null && !selfEliminated)
            {
                customerQueue.Apply(state);
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
            // 「怒り → 退店」で帰す。行列から消えた事実は state 側で分かるが、
            // 提供済みか我慢切れかはこの通知でしか判別できない。
            customerQueue?.MarkLeft(customerId);

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
            // 「喜び → 退店」で帰す。
            customerQueue?.MarkServed(customerId);
        }

        public void OnPhaseChanged(Phase phase)
        {
        }

        public void OnForcedEliminationWarning(int untilTick, double thresholdPct)
        {
        }

        public void OnStoreEliminated(string storeId, EliminationReason reason, int finalRank)
        {
            resultView?.RecordElimination(storeId, finalRank);

            if (store != null && storeId == store.State.SelfStoreId)
            {
                // 自店が脱落したら行列を畳む。以降は観戦なので自店に客は来ない。
                selfEliminated = true;
                customerQueue?.ClearAll();
                patienceTimer?.Stop();
                resultView?.Show(finalRank);
            }
        }

        public void OnMatchEnd(int finalRank, MatchStats stats)
        {
            customerQueue?.ClearAll();
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

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
using Takoda99.View.ValueObjects;
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
        [SerializeField] private RankBarView rankBar;
        [SerializeField] private Customers.CustomerQueueView customerQueue;
        [SerializeField] private Customers.CustomerOrderBubbleView orderBubble;
        [SerializeField] private StarRatingView starRating;
        [SerializeField] private GameBeforeView gameBefore;

        private IStore store;
        private ITypingJudge typingJudge;
        private IDisposable subscription;
        private string servingCustomerId;
        private bool subStoreBoardBound;

        /// <summary>自店が脱落済みか。以降は観戦なので行列を描かない。</summary>
        private bool selfEliminated;

        /// <summary>IStore / ITypingJudge を注入する（01-renderer.md §3）。通常は OnEnable が自動で呼ぶ。</summary>
        public void Bind(IStore boundStore, ITypingJudge boundTypingJudge)
        {
            subscription?.Dispose();

            store = boundStore;
            typingJudge = boundTypingJudge;
            subStoreBoardBound = false;
            selfEliminated = false;

            if (gameBefore != null)
            {
                // 数え終わりは state 変化と一致しないため、明けた瞬間に描き直す。
                gameBefore.Finished -= HandleGameBeforeFinished;
                gameBefore.Finished += HandleGameBeforeFinished;
                gameBefore.Begin();
            }

            subscription = store.Subscribe(HandleStateChanged);
            HandleStateChanged(store.State);
        }

        /// <summary>待機が明けた。この瞬間に現在の state をそのまま描き直し、お題と行列を出す。</summary>
        private void HandleGameBeforeFinished()
        {
            if (store != null)
            {
                HandleStateChanged(store.State);
            }
        }

        private void Awake()
        {
            // 未割り当ての参照は「その機能だけが黙って動かない」形で表面化する。
            // 画面が出ない原因を探すより、起動時に名指しで知らせるほうが早い。
            WarnIfMissing(mainStore, nameof(mainStore));
            WarnIfMissing(subStoreBoard, nameof(subStoreBoard));
            WarnIfMissing(patienceTimer, nameof(patienceTimer));
            WarnIfMissing(rankBar, nameof(rankBar));
            WarnIfMissing(customerQueue, nameof(customerQueue));
            WarnIfMissing(orderBubble, nameof(orderBubble));
            WarnIfMissing(starRating, nameof(starRating));
            WarnIfMissing(gameBefore, nameof(gameBefore));

            // 試合終了後の Result シーンへの遷移は、このモーダルの NextButton だけが担う
            // （GameBootstrapper は MainGame にいる間は自動遷移しない）。未割り当てだと
            // 試合が終わっても MainGame から出られなくなるため、他より強く知らせる。
            if (resultView == null)
            {
                Debug.LogError($"{nameof(Renderer)}.{nameof(resultView)} が未割り当てです。試合終了後に Result シーンへ進めなくなります。", this);
            }
        }

        private void WarnIfMissing(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                Debug.LogWarning($"{nameof(Renderer)}.{fieldName} が未割り当てです。この要素は描画されません。", this);
            }
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

            if (gameBefore != null)
            {
                gameBefore.Finished -= HandleGameBeforeFinished;
            }

            subscription?.Dispose();
        }

        private void HandleStateChanged(ClientState state)
        {
            // ★このメソッドの先頭で行う。
            // 試合終了の検知は state を正とし、OnMatchEnd（IRenderer コールバック）には依存しない。
            // Dispatcher は `_store.Apply(action)` → `OnActionApplied?.Invoke(action)` の順で走るため、
            // store のリスナー（Store.Notify はリスナー単位の例外処理を持たない）のどれか1つが
            // 例外を投げると OnActionApplied ごと落ち、OnMatchEnd が永久に呼ばれない。
            // それをモーダル表示の唯一の契機にしていたのが「優勝してもモーダルが出ない」の原因。
            // しかも自動シーン遷移も止めたため、その場合 MainGame から出られなくなる。
            //
            // state.Result は MatchEnd でしか入らない（Reducer）ので、条件としてはこれで十分。
            // 描画処理より前に置くことで、後続で何が起きてもモーダルだけは必ず出る。
            if (state.Phase == ClientPhase.Result && state.Result != null)
            {
                resultView?.ShowIfHidden(state.Result.FinalRank);
            }

            // 試合開始の合図はサーバー（MatchStart ＝ InMatch 到達）。カウントダウンが
            // 0 になっていても、これが届くまで待機画面は畳まない。
            if (gameBefore != null)
            {
                gameBefore.SetMatchStarted(state.Phase == ClientPhase.InMatch || state.Phase == ClientPhase.Spectating);
            }

            // 待機中はお題も行列も出さない。サーバーは待たずに配信してくる可能性があるため、
            // state はそのまま溜め、明けた瞬間に HandleGameBeforeFinished から描き直す。
            var holding = gameBefore != null && gameBefore.IsHolding;

            if (mainStore != null)
            {
                mainStore.SetCreditLife(state.CreditLife);
                mainStore.SetEvaluation(state.Normalized, state.Alive);
                mainStore.SetPlayerName(FindSelfDisplayName(state));

                if (holding)
                {
                    mainStore.SetWord(string.Empty, string.Empty);
                    mainStore.SetOrderProgress(0, 0);
                }
                else
                {
                    ApplyWord(state);
                    ApplyOrderCounter(state);
                }
            }

            if (starRating != null)
            {
                // 星は受信値そのまま（EvaluationUpdate.starRating）。ここで再計算しない。
                starRating.SetRating(state.StarRating);
            }

            if (subStoreBoard != null)
            {
                subStoreBoard.SetAliveCount(state.AliveCount);

                var others = state.Stores.Where(s => s.StoreId != state.SelfStoreId).ToList();

                if (!subStoreBoardBound && others.Count > 0)
                {
                    subStoreBoard.Bind(others.Select(s => s.StoreId).ToList());
                    subStoreBoardBound = true;
                }

                foreach (var summary in others)
                {
                    subStoreBoard.SetSummary(summary.StoreId, summary.CreditLife, summary.Alive);
                    subStoreBoard.SetDisplayName(summary.StoreId, summary.DisplayName);
                    if (summary.FinalRank.HasValue)
                    {
                        subStoreBoard.SetRank(summary.StoreId, summary.FinalRank.Value);
                    }
                }
            }

            if (rankBar != null)
            {
                rankBar.SetState(RankBarViewState.From(state.Rank, state.AliveCount, state.Params.MaxStores, state.Params.StormThresholdPct));
            }

            // 行列の描画。ここを呼ばないと、サーバー由来の客が state.Queue に溜まるだけで
            // 画面に一切出ない（この結線漏れが「客がテストドライバ由来になっていた」原因）。
            // 自店が脱落した後は観戦なので、state.Queue に何が残っていても行列は描かない。
            if (customerQueue != null && !selfEliminated && !holding)
            {
                customerQueue.Apply(state);
            }

            if (!holding)
            {
                ApplyServingCustomer(state);
            }
        }

        private void ApplyWord(ClientState state)
        {
            if (state.CurrentOrder is null || typingJudge is null)
            {
                mainStore.SetWord(string.Empty, string.Empty);
                return;
            }

            var view = typingJudge.CurrentView;
            mainStore.SetWord(view.CurrentWord, view.CurrentRoma);
            mainStore.SetTypedProgress(view.TypedKanaLength, view.TypedRomaLength);
        }

        /// <summary>
        /// 注文カウンタ。分子は準備できたたこ焼きの数（＝打ち終えた単語数 <c>WordIndex</c>）、
        /// 分母は注文個数。
        /// </summary>
        /// <remarks>
        /// 分母は「行列の先頭の客」から引く。<c>CurrentOrder</c> だけを見ると、前の客が帰ってから
        /// 次の客の打鍵が始まるまでの間だけ 0/0 に落ち、注文数の表示が客の入れ替わりから遅れて見える。
        /// 先頭が入れ替わった瞬間に新しい注文数へ切り替わるようにする。
        /// </remarks>
        private void ApplyOrderCounter(ClientState state)
        {
            var front = state.Queue.Count > 0 ? state.Queue[0] : null;

            if (front is null)
            {
                mainStore.SetOrderProgress(0, 0);
                return;
            }

            // 対応中の注文が先頭客のものならその進捗を、まだ始まっていなければ 0 個目として出す。
            var prepared = state.CurrentOrder is not null && state.CurrentOrder.CustomerId == front.View.CustomerId
                ? state.CurrentOrder.WordIndex
                : 0;

            mainStore.SetOrderProgress(prepared, front.View.OrderCount);
        }

        /// <summary>自店の表示名。StoreListUpdate が届くまでは空になる（受信値をそのまま使う）。</summary>
        private static string FindSelfDisplayName(ClientState state)
        {
            foreach (var summary in state.Stores)
            {
                if (summary.StoreId == state.SelfStoreId)
                {
                    return summary.DisplayName;
                }
            }

            return string.Empty;
        }

        private void ApplyServingCustomer(ClientState state)
        {
            var front = state.Queue.Count > 0 ? state.Queue[0] : null;
            var frontId = front?.View.CustomerId;

            if (frontId == servingCustomerId)
            {
                return;
            }

            servingCustomerId = frontId;

            if (front is null)
            {
                patienceTimer?.Stop();
                orderBubble?.Hide();
                return;
            }

            // 我慢は「先頭に来て注文した瞬間」から減り始める。行列に並び始めた時刻（ArrivedAtLocalMs）を
            // 起点にすると、待たされていた客ほど先頭に来た時点で既にゲージが減っており、
            // 前の客に提供し終えた直後からゲージが尽きたままライフだけが減る。
            var nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d);
            patienceTimer?.Stop();
            patienceTimer?.Begin(nowMs, front.View.PatienceMaxMs);

            // 先頭に来た瞬間に注文文句を出す。文面は契約に無いため個数から組み立てる
            // （サーバーが文面を配信するようになったら第3引数に渡すだけでよい）。
            orderBubble?.Show(front.View.CustomerId, front.View.OrderCount);
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
                orderBubble?.Hide();
                resultView?.Show(finalRank);
            }
        }

        public void OnMatchEnd(int finalRank, MatchStats stats)
        {
            customerQueue?.ClearAll();
            orderBubble?.Hide();
            patienceTimer?.Stop();

            // 最後まで生き残った店（1位）には OnStoreEliminated が来ない。そのため
            // 自店を順位一覧（上位10店）へ載せられるのはここだけになる。
            if (!selfEliminated)
            {
                selfEliminated = true;
                var selfStoreId = store?.State.SelfStoreId;
                if (!string.IsNullOrEmpty(selfStoreId))
                {
                    resultView?.RecordElimination(selfStoreId, finalRank);
                }
            }

            // モーダル自体は HandleStateChanged（state 駆動）が先に出していることもある。
            // ShowIfHidden は冪等なので、どちらが先でも順位を上書きせず二重表示にもならない。
            resultView?.ShowIfHidden(finalRank);
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

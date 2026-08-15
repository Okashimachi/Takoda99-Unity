// 仕様書: Unity/docs/.sdd/hud/01-hud-composition.md（本選 v0.8.0）
//         Unity/docs/.sdd/match-view/01-renderer.md（予選版。矛盾したら hud/01 が優先）
// IRenderer の Unity 実体。MatchClientController からの離散イベントと IStore の連続的な状態変化を
// 下位View（MainStoreView / SelfRankView / RankingPanelView / CullCountdownPanelView 等）へ振り分ける。
//
// 値の決定・推定はしない（受信値を描くだけ）。スコアから順位を計算しない（state.Rank が権威）。

using System;
using System.Collections.Generic;
using Takoda99.Client.Lifecycle;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary><see cref="IRenderer"/> の Unity 実体（hud/01-hud-composition.md §4）。</summary>
    public sealed class Renderer : MonoBehaviour, IRenderer
    {
        [SerializeField] private MainStoreView mainStore;
        [SerializeField] private EliminationResultView resultView;
        [SerializeField] private Customers.CustomerQueueView customerQueue;
        [SerializeField] private Customers.CustomerOrderBubbleView orderBubble;
        [SerializeField] private GameBeforeView gameBefore;

        [Header("本選 HUD")]
        [SerializeField] private SelfRankView selfRank;                        // 順位の大表示＋スコア＋生存数
        [SerializeField] private Ranking.RankingPanelView rankingPanel;        // ranking-view/01
        [SerializeField] private Ranking.CullCountdownPanelView cullPanel;     // ranking-view/02
        [SerializeField] private Elimination.MassEliminationEffect massElim;   // elimination/01

        private IStore store;
        private ITypingJudge typingJudge;
        private IDisposable subscription;
        private string servingCustomerId;

        /// <summary>自店が脱落済みか。以降は観戦なので行列を描かない。</summary>
        private bool selfEliminated;

        /// <summary>IStore / ITypingJudge を注入する。通常は OnEnable が自動で呼ぶ。</summary>
        public void Bind(IStore boundStore, ITypingJudge boundTypingJudge)
        {
            subscription?.Dispose();

            store = boundStore;
            typingJudge = boundTypingJudge;
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
            WarnIfMissing(customerQueue, nameof(customerQueue));
            WarnIfMissing(orderBubble, nameof(orderBubble));
            WarnIfMissing(gameBefore, nameof(gameBefore));
            WarnIfMissing(selfRank, nameof(selfRank));
            WarnIfMissing(rankingPanel, nameof(rankingPanel));
            WarnIfMissing(cullPanel, nameof(cullPanel));
            WarnIfMissing(massElim, nameof(massElim));

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
            //
            // v0.8.0 では MatchEnd が空ペイロードになり state.Result が消えたため、条件を
            // state.MatchEnded へ差し替える。順位は PersonalResult から取る。
            // PersonalResult が未受信でも rank=0 でモーダルを出す：
            // **試合が終わったのに画面から出られない状態を作らない**ことが、この一線の目的。
            if (state.MatchEnded && resultView != null)
            {
                var rank = state.PersonalResult != null ? state.PersonalResult.FinalRank : 0;

                // Result フェーズ中は state 変化のたびここへ来る。ログは実際に出す瞬間だけに絞る。
                if (!resultView.IsShown)
                {
                    Debug.Log($"{nameof(Renderer)}: MatchEnd 受信 rank={rank}。リザルトモーダルを表示します。", this);
                }

                resultView.ShowIfHidden(rank);
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
                mainStore.SetPlayerName(ResolveSelfDisplayName(state));

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

            // 自店の順位・スコア・生存数。**順位が本選の画面の主役**。
            selfRank?.SetState(SelfRankViewState.From(state.Rank, state.Score, state.AliveCount));

            // ランキング（上位N＋自分）。待機中は描かない。
            if (rankingPanel != null)
            {
                if (holding)
                {
                    rankingPanel.SetPanelVisible(false);
                }
                else
                {
                    rankingPanel.Apply(state);
                }
            }

            // 足切り予告。秒読みの毎フレーム更新はパネル側の Update が行う。
            // ここは受信値の差し替えのみ（ClientState に経過時間を書き戻さない）。
            if (cullPanel != null)
            {
                if (holding)
                {
                    cullPanel.SetPanelVisible(false);
                }
                else
                {
                    cullPanel.SetWarning(state.Cull, state);
                }
            }

            // 行列の描画。ここを呼ばないと、サーバー由来の客が state.Queue に溜まるだけで
            // 画面に一切出ない。自店が脱落した後は観戦なので行列は描かない。
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

        /// <summary>
        /// 自店の表示名。MatchStart のキャッシュから引く（表示名を配るのは MatchStart だけで、
        /// StoreListUpdate は v0.8.0 で廃止された）。
        /// </summary>
        private static string ResolveSelfDisplayName(ClientState state)
            => state.DisplayNames.TryGetValue(state.SelfStoreId, out var name) ? name : string.Empty;

        /// <summary>
        /// 先頭客が入れ替わったら注文吹き出しを出し直す。
        /// 我慢ゲージが廃止されたため、ここは吹き出しの出し入れだけになる。
        /// </summary>
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
                orderBubble?.Hide();
                return;
            }

            // 先頭に来た瞬間に注文文句を出す。文面は契約に無いため個数から組み立てる
            // （サーバーが文面を配信するようになったら第3引数に渡すだけでよい）。
            orderBubble?.Show(front.View.CustomerId, front.View.OrderCount);
        }

        // ── IRenderer ────────────────────────────────────────────────

        public void OnCustomerArrived(CustomerView customer)
        {
            // 対応中客の検知は HandleStateChanged 側で行う。
        }

        public void OnKeyFeedback(KeyResult result)
        {
        }

        public void OnOrderServed(string customerId)
        {
            // 「喜び → 退店」で帰す。本選では客が減る契機はこれだけ（離脱は廃止）。
            customerQueue?.MarkServed(customerId);
        }

        public void OnPhaseChanged(Phase phase)
        {
        }

        /// <summary>
        /// 足切りの予告。値の描画は state 駆動側（<see cref="HandleStateChanged"/>）が行うため、
        /// ここは**受信の瞬間だけ必要な演出**（自分が対象圏に入った瞬間のアラート等）に使う。
        /// </summary>
        public void OnCullWarning(CullWarning warning)
        {
            cullPanel?.OnWarningReceived(warning);
        }

        public void OnStoreEliminatedBatch(int stageIndex, IReadOnlyList<StoreEliminated> entries, bool includesSelf)
        {
            if (entries.Count == 0)
            {
                return;
            }

            // ★entries を1件ずつループして演出を呼ばない。件数だけを渡す。
            // 個々の storeId が要るのはランキング表示側であり、そちらは state 経由で更新済み。
            massElim?.Play(stageIndex, entries.Count, includesSelf);

            if (!includesSelf)
            {
                return;
            }

            // 自店の脱落。この時点ではリザルトへ行かない（120秒の MatchEnd を待つ）。
            selfEliminated = true;
            customerQueue?.ClearAll();
            orderBubble?.Hide();

            // 最終ステージ（120秒）では直後に MatchEnd が来る。その場合は脱落モーダルではなく
            // リザルトへ進むため、ここでは出さない。判定を state だけで閉じる。
            if (store != null && store.State.MatchEnded)
            {
                return;
            }

            var selfFinalRank = FindSelfFinalRank(entries, store?.State.SelfStoreId);

            // ★優勝者に脱落モーダルを出さない。
            // 本選は120秒で1位も含む全店に StoreEliminatedBatch が飛ぶ（優勝＝最後まで残ったことの
            // 表現であって、脱落イベント自体は全員に来る）。MatchEnd の到着を待って抑止する作りだと、
            // batch → MatchEnd の順に届く間だけ優勝者に「脱落」が一瞬見える。順序に依存せず、
            // finalRank だけで閉じる（リザルト演出の分岐基準 ResultTierRule と同じ考え方）。
            if (selfFinalRank == 1)
            {
                return;
            }

            resultView?.Show(selfFinalRank);
        }

        /// <summary>個人成績の保持は Store の責務。画面に出すのは個人成績シーン。</summary>
        public void OnPersonalResult(PersonalResultState result)
        {
        }

        public void OnMatchEnd()
        {
            // MatchEnd が届いたこと自体を必ず1回残す。1試合に1回しか来ないためログとしても静か。
            // このログが出ない＝MatchEnd がクライアントまで届いていない（もしくは Dispatcher の
            // OnActionApplied が手前で落ちている）と切り分けられる。
            var rank = store?.State.PersonalResult?.FinalRank ?? 0;
            Debug.Log($"{nameof(Renderer)}.{nameof(OnMatchEnd)}: MatchEnd 受信 rank={rank}", this);

            selfEliminated = true;
            customerQueue?.ClearAll();
            orderBubble?.Hide();
            rankingPanel?.SetPanelVisible(false);
            cullPanel?.SetPanelVisible(false);

            // モーダル自体は HandleStateChanged（state 駆動）が先に出していることもある。
            // ShowIfHidden は冪等なので、どちらが先でも順位を上書きせず二重表示にもならない。
            resultView?.ShowIfHidden(rank);
        }

        public void OnLifecycleChanged(ClientPhase from, ClientPhase to)
        {
        }

        public void OnConnectionTrouble(string kind)
        {
            Debug.LogWarning($"{nameof(Renderer)}: connection trouble ({kind})", this);
        }

        private static int FindSelfFinalRank(IReadOnlyList<StoreEliminated> entries, string selfStoreId)
        {
            if (string.IsNullOrEmpty(selfStoreId))
            {
                return 0;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].StoreId == selfStoreId)
                {
                    return entries[i].FinalRank;
                }
            }

            return 0;
        }
    }
}

// 仕様書: pureC#/docs/.sdd/value-objects/02-store-state.md
// 自店舗の詳細。評価3段階・脱落演出等の表示用派生状態は持たない（Unity側 value-objects の責務）。

using System.Collections.Generic;
using Takoda99.Proto;

namespace Takoda99.Client.State
{
    /// <summary>
    /// 自店舗の詳細。サーバーから届いた事実のみを保持する。
    /// </summary>
    public readonly record struct StoreState(
        string StoreId,
        string DisplayName,
        int CreditLife,
        double EvalRaw,
        double EvalNormalized, // 0..1。EvaluationUpdate では normalized、StoreSummary では evalNormalized（SV-09）
        int Rank,
        bool Alive,
        IReadOnlyList<string> StoreQueue // CustomerId の並び。先頭が対応中。クライアントがローカル構築する
    )
    {
        /// <summary>
        /// <c>MatchStart</c> から自店の初期状態を生成する。
        /// 信用ライフは <c>params.initialLife</c>、それ以外は自店の <c>StoreSummary</c> に従う。
        /// 行列は配信されないため空で始める（SV-04）。
        /// </summary>
        public static StoreState FromMatchStart(MatchStart message)
        {
            var self = FindStore(message.Stores, message.SelfStoreId);
            return new StoreState(
                StoreId: message.SelfStoreId,
                DisplayName: self?.DisplayName ?? string.Empty,
                CreditLife: message.Params.InitialLife,
                EvalRaw: 0d,
                EvalNormalized: self?.EvalNormalized ?? 0d,
                Rank: self?.Rank ?? 0,
                Alive: self?.Alive ?? true,
                StoreQueue: new string[0]);
        }

        /// <summary>自店専用メッセージ。評価・順位を受信値で置換する。</summary>
        public StoreState Apply(EvaluationUpdate message) => this with
        {
            EvalRaw = message.EvalRaw,
            EvalNormalized = message.Normalized,
            Rank = message.Rank,
        };

        /// <summary>
        /// 自店専用メッセージ。確定値 <c>life</c> で置換する。
        /// <c>delta</c> は演出のトリガー情報であり、クライアントで加減算して値を作らない。
        /// </summary>
        public StoreState Apply(CreditUpdate message) => this with { CreditLife = message.Life };

        /// <summary>自店が対象の場合のみ生存フラグを落とす。他店は <see cref="StoreSummaryState"/> 側で扱う。</summary>
        public StoreState Apply(StoreEliminated message) =>
            message.StoreId == StoreId ? this with { Alive = false } : this;

        /// <summary>行列末尾へ客を追加する（<c>CustomerArrived</c>）。</summary>
        public StoreState WithCustomerEnqueued(string customerId)
        {
            var queue = new List<string>(StoreQueue) { customerId };
            return this with { StoreQueue = queue };
        }

        /// <summary>行列から客を取り除く（<c>CustomerLeft</c> / 提供完了）。存在しなければ何もしない。</summary>
        public StoreState WithCustomerDequeued(string customerId)
        {
            var queue = new List<string>(StoreQueue);
            if (!queue.Remove(customerId))
            {
                return this;
            }

            return this with { StoreQueue = queue };
        }

        /// <summary>対応中（行列先頭）の客。行列が空なら null。</summary>
        public string? CurrentCustomerId => StoreQueue.Count > 0 ? StoreQueue[0] : null;

        internal static StoreSummary? FindStore(List<StoreSummary> stores, string storeId)
        {
            if (stores == null)
            {
                return null;
            }

            foreach (var store in stores)
            {
                if (store.StoreId == storeId)
                {
                    return store;
                }
            }

            return null;
        }
    }
}

// 仕様書: pureC#/docs/.sdd/value-objects/02-store-state.md
// 99店ミニ盤面用の全店サマリー。他店の状況は MatchStart.stores の初期スナップからのみ得られる
// （v0.8.0 以降の試合中の配信は RankingSnapshot / RankingDelta が担う）。

using System.Collections.Generic;
using Takoda99.Proto;

namespace Takoda99.Client.State
{
    /// <summary>Proto の <see cref="StoreSummary"/> に対応する、99店ミニ盤面用の1店ぶんの状態。</summary>
    public readonly record struct StoreSummaryState(
        string StoreId,
        string DisplayName,
        int Score, // 順位を決める累積値（v0.8.0・本選）
        int Rank,
        bool Alive,
        int? FinalRank // 脱落済みの店のみ。null は「まだ脱落していない」（Proto v0.3.0）
    )
    {
        public static StoreSummaryState From(StoreSummary summary)
        {
            return new StoreSummaryState(
                StoreId: summary.StoreId,
                DisplayName: summary.DisplayName,
                Score: summary.Score,
                Rank: summary.Rank,
                Alive: summary.Alive,
                FinalRank: summary.FinalRank);
        }

        /// <summary>
        /// <c>MatchStart.stores</c> のフルスナップから全店ぶんを生成する。
        /// 件数が <c>params.maxStores</c> と一致しない（欠員あり）場合でも、そのまま受信件数を保持する。
        /// </summary>
        public static IReadOnlyList<StoreSummaryState> FromAll(List<StoreSummary> stores)
        {
            if (stores == null)
            {
                return new StoreSummaryState[0];
            }

            var result = new List<StoreSummaryState>(stores.Count);
            foreach (var store in stores)
            {
                result.Add(From(store));
            }

            return result;
        }

        /// <summary>
        /// <c>StoreEliminated</c> を全店サマリーへ適用する。対象店のみ <c>Alive = false</c> になる。
        /// </summary>
        public static IReadOnlyList<StoreSummaryState> ApplyEliminated(
            IReadOnlyList<StoreSummaryState> summaries, StoreEliminated message)
        {
            var result = new List<StoreSummaryState>(summaries.Count);
            foreach (var summary in summaries)
            {
                result.Add(summary.StoreId == message.StoreId
                    ? summary with { Alive = false, FinalRank = message.FinalRank }
                    : summary);
            }

            return result;
        }
    }
}

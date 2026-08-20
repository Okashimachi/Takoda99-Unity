// 仕様書: Unity/docs/.sdd/value-objects/12-ranking-row-style.md §4.2 / hud/01-hud-composition.md §5.1
// 自店の配色区分（Tone）を state から引く共通処理。
//
// SelfRankView（順位テキスト）と SelfRankNeonPanelView（右下のネオンパネル）が同じ根拠で
// 色を決めるために切り出している。**同じ判定を2箇所に書くと、片方だけ直したときに
// 画面上で自店の色が食い違う**（HUDは赤いのにパネルは白い等）。
//
// 順位・スコア・淘汰を計算しない。危険の根拠はサーバーの CutStoreIds と
// 下位パネルの表示範囲だけで、Rank と CutLineRank を比較しない（ranking-view/02 §1）。

using System;
using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;

namespace Takoda99.View.Ranking
{
    /// <summary>自店の Tone を state から決める。色そのものは持たない（RankingRowPalette から引く）。</summary>
    public static class SelfRankToneResolver
    {
        /// <summary>
        /// 「ぎりぎり圏外（AtRisk）」の判定に使う下位の件数の既定値。
        /// **BottomRankingPanelView.visibleCount と揃える**（画面上の警告帯と自店の色が一致するように）。
        /// </summary>
        public const int DefaultBottomRangeCount = 30;

        /// <summary>state から自店の Tone を決める。</summary>
        /// <param name="bottomRangeCount">下位パネルの表示件数（<see cref="DefaultBottomRangeCount"/>）。</param>
        public static RankingRowTone Resolve(ClientState state, int bottomRangeCount)
        {
            if (state == null)
            {
                return RankingRowTone.Normal;
            }

            var isCutTarget = ContainsStoreId(state.Cull?.CutStoreIds, state.SelfStoreId);

            var isInBottomRange = RankingRowsBuilder.IsInBottomRange(
                state.Ranking, state.SelfStoreId, state.AliveCount, bottomRangeCount);

            return RankingRowStyle.ResolveSelfRankTone(state.Rank, state.Alive, isCutTarget, isInBottomRange);
        }

        /// <summary>storeId の一覧に含まれるか。LINQ を使わない（WebGL の GC を避ける）。</summary>
        public static bool ContainsStoreId(IReadOnlyList<string> list, string storeId)
        {
            if (list == null || string.IsNullOrEmpty(storeId))
            {
                return false;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], storeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

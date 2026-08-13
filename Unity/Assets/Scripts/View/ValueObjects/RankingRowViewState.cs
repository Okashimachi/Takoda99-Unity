// 仕様書: Unity/docs/.sdd/value-objects/08-ranking-row-view-state.md
// ランキング表示の派生状態。Store から導出し、View がそのまま描ける形へ変換するだけ。
// 順位・スコアの計算はしない（サーバー権威）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（Unity のスクリプティングランタイム制約）。

using System;
using System.Collections.Generic;
using Takoda99.Client.State;

namespace Takoda99.View.ValueObjects
{
    /// <summary>ランキング表の1行。</summary>
    public readonly struct RankingRowViewState : IEquatable<RankingRowViewState>
    {
        /// <summary>順位が未確定（0以下）のときに出す表記。0位は存在しない。</summary>
        public const string UnknownRankText = "--";

        public string StoreId { get; }

        /// <summary>"1" / "--"。構築時に1回だけ ToString する。</summary>
        public string RankText { get; }

        /// <summary>表示名。空なら StoreId（空欄にしない）。</summary>
        public string NameText { get; }

        /// <summary>"1200" / "-30"。**0でクランプしない**。</summary>
        public string ScoreText { get; }

        public bool IsSelf { get; }

        public bool IsAlive { get; }

        private RankingRowViewState(string storeId, string rankText, string nameText, string scoreText, bool isSelf, bool isAlive)
        {
            StoreId = storeId;
            RankText = rankText;
            NameText = nameText;
            ScoreText = scoreText;
            IsSelf = isSelf;
            IsAlive = isAlive;
        }

        /// <summary>ランキング表の1行から作る（他人の行）。</summary>
        public static RankingRowViewState From(RankingRow row, bool isSelf)
        {
            if (row == null)
            {
                return default;
            }

            return Create(row.StoreId, row.Rank, row.Score, row.DisplayName, isSelf, row.Alive);
        }

        /// <summary>
        /// 自分の行。順位・スコアは EvaluationUpdate 由来の権威値で上書きする。
        /// Ranking は RankingDelta の取りこぼしでズレ得るため、自分の値だけは権威値に差し替える
        /// （「1位のはずなのに3位と出ている」は体験として最悪）。
        /// </summary>
        public static RankingRowViewState FromSelf(RankingRow row, int authoritativeRank, int authoritativeScore)
        {
            if (row == null)
            {
                return default;
            }

            return Create(row.StoreId, authoritativeRank, authoritativeScore, row.DisplayName, true, row.Alive);
        }

        /// <summary>自分が Ranking に居ない場合に、権威値だけで行を作る。</summary>
        public static RankingRowViewState SelfOnly(string storeId, int authoritativeRank, int authoritativeScore, string displayName)
            => Create(storeId, authoritativeRank, authoritativeScore, displayName, true, true);

        private static RankingRowViewState Create(string storeId, int rank, int score, string displayName, bool isSelf, bool isAlive)
        {
            var id = storeId ?? string.Empty;
            return new RankingRowViewState(
                id,
                rank >= 1 ? rank.ToString() : UnknownRankText,
                string.IsNullOrEmpty(displayName) ? id : displayName,
                score.ToString(),
                isSelf,
                isAlive);
        }

        /// <summary>
        /// 99行のリストで「値が変わった行だけ TMP を更新する」ために実装している
        /// （ranking-view/03 §4 P2）。等しければ TMP.text への代入ごと省く。
        /// </summary>
        public bool Equals(RankingRowViewState other)
        {
            return IsSelf == other.IsSelf
                && IsAlive == other.IsAlive
                && string.Equals(StoreId, other.StoreId, StringComparison.Ordinal)
                && string.Equals(RankText, other.RankText, StringComparison.Ordinal)
                && string.Equals(NameText, other.NameText, StringComparison.Ordinal)
                && string.Equals(ScoreText, other.ScoreText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is RankingRowViewState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StoreId != null ? StoreId.GetHashCode() : 0;
                hash = (hash * 397) ^ (RankText != null ? RankText.GetHashCode() : 0);
                hash = (hash * 397) ^ (NameText != null ? NameText.GetHashCode() : 0);
                hash = (hash * 397) ^ (ScoreText != null ? ScoreText.GetHashCode() : 0);
                hash = (hash * 397) ^ (IsSelf ? 1 : 0);
                hash = (hash * 397) ^ (IsAlive ? 1 : 0);
                return hash;
            }
        }
    }

    /// <summary>自店HUD（順位の大表示＋スコア＋生存数）の表示用状態。</summary>
    public readonly struct SelfRankViewState : IEquatable<SelfRankViewState>
    {
        public string RankText { get; }        // "12" / "--"

        public string ScoreText { get; }       // "1200" / "-30"

        public string AliveCountText { get; }  // "残り 55 店"

        private SelfRankViewState(string rankText, string scoreText, string aliveCountText)
        {
            RankText = rankText;
            ScoreText = scoreText;
            AliveCountText = aliveCountText;
        }

        public static SelfRankViewState From(int rank, int score, int aliveCount)
        {
            return new SelfRankViewState(
                rank >= 1 ? rank.ToString() : RankingRowViewState.UnknownRankText,
                score.ToString(),
                // 0店は表示しない（試合前・未受信と区別がつかないため）。
                aliveCount >= 1 ? "残り " + aliveCount + " 店" : string.Empty);
        }

        public bool Equals(SelfRankViewState other)
        {
            return string.Equals(RankText, other.RankText, StringComparison.Ordinal)
                && string.Equals(ScoreText, other.ScoreText, StringComparison.Ordinal)
                && string.Equals(AliveCountText, other.AliveCountText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is SelfRankViewState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RankText != null ? RankText.GetHashCode() : 0;
                hash = (hash * 397) ^ (ScoreText != null ? ScoreText.GetHashCode() : 0);
                hash = (hash * 397) ^ (AliveCountText != null ? AliveCountText.GetHashCode() : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// 表示行の組み立て。<c>RankingPanelView.Apply</c> の中身を、テストできる純関数として切り出したもの。
    /// </summary>
    public static class RankingRowsBuilder
    {
        /// <summary>表示件数の下限。100秒時点の生存数が10人＝上位10名リストが生存者全員になるため。</summary>
        public const int MinVisibleCount = 10;

        /// <summary>
        /// 上位 visibleCount 件を取り、自分が含まれていなければ末尾に自分を足す。
        /// 自分の行は authoritativeRank / authoritativeScore で上書きする。
        /// </summary>
        public static IReadOnlyList<RankingRowViewState> Build(
            RankingTable ranking,
            string selfStoreId,
            int authoritativeRank,
            int authoritativeScore,
            int visibleCount)
        {
            // B3: 空なら空リスト（パネルごと非表示にする合図）。
            if (ranking == null || ranking.Rows.Count == 0)
            {
                return Array.Empty<RankingRowViewState>();
            }

            // B1: 10 未満は 10 にクランプする。
            var count = visibleCount < MinVisibleCount ? MinVisibleCount : visibleCount;
            var top = ranking.Top(count);

            var result = new List<RankingRowViewState>(top.Count + 1);
            var selfIncluded = false;

            // B5: ranking.Rows の順を保つ（再ソートしない）。
            for (var i = 0; i < top.Count; i++)
            {
                var row = top[i];
                var isSelf = IsSelf(row.StoreId, selfStoreId);
                if (isSelf)
                {
                    selfIncluded = true;
                    result.Add(RankingRowViewState.FromSelf(row, authoritativeRank, authoritativeScore));
                }
                else
                {
                    result.Add(RankingRowViewState.From(row, false));
                }
            }

            // B2: 自分が上位に含まれるなら足さない（重複させない）。
            if (selfIncluded || string.IsNullOrEmpty(selfStoreId))
            {
                return result;
            }

            var selfRow = ranking.Find(selfStoreId);
            result.Add(selfRow != null
                ? RankingRowViewState.FromSelf(selfRow, authoritativeRank, authoritativeScore)
                // B4: 自分が ranking に居なくても、権威値だけで行を作って足す。
                : RankingRowViewState.SelfOnly(selfStoreId, authoritativeRank, authoritativeScore, string.Empty));

            return result;
        }

        /// <summary>観戦画面用。全行をそのまま変換する（自分だけ権威値で上書き）。</summary>
        public static IReadOnlyList<RankingRowViewState> BuildAll(
            RankingTable ranking,
            string selfStoreId,
            int authoritativeRank,
            int authoritativeScore)
        {
            if (ranking == null || ranking.Rows.Count == 0)
            {
                return Array.Empty<RankingRowViewState>();
            }

            var rows = ranking.Rows;
            var result = new List<RankingRowViewState>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                result.Add(IsSelf(row.StoreId, selfStoreId)
                    ? RankingRowViewState.FromSelf(row, authoritativeRank, authoritativeScore)
                    : RankingRowViewState.From(row, false));
            }

            return result;
        }

        private static bool IsSelf(string storeId, string selfStoreId)
            => !string.IsNullOrEmpty(selfStoreId) && string.Equals(storeId, selfStoreId, StringComparison.Ordinal);
    }
}

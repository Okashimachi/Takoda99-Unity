// 仕様書: Unity/docs/.sdd/value-objects/11-rank-ordinal.md
// 順位 → 序数表記。1〜99 を事前計算した表から引くだけ（実行時に文字列を作らない）。

namespace Takoda99.View.ValueObjects
{
    /// <summary>順位 → 序数表記。1〜99 を事前計算した表から引くだけ（実行時に文字列を作らない）。</summary>
    public static class RankOrdinal
    {
        /// <summary>順位が未確定（0以下）／範囲外のときの表記。</summary>
        public const string Unknown = RankingRowViewState.UnknownRankText; // "--"

        /// <summary>最大順位。99店固定（GameParametersPublicSubset.MaxStores）。</summary>
        public const int MaxRank = 99;

        private static readonly string[] Table = BuildTable();

        private static string[] BuildTable()
        {
            var table = new string[MaxRank + 1];
            for (var rank = 1; rank <= MaxRank; rank++)
            {
                table[rank] = Build(rank);
            }

            return table;
        }

        private static string Build(int rank)
        {
            var lastTwoDigits = rank % 100;
            if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
            {
                return rank + "th";
            }

            switch (rank % 10)
            {
                case 1:
                    return rank + "st";
                case 2:
                    return rank + "nd";
                case 3:
                    return rank + "rd";
                default:
                    return rank + "th";
            }
        }

        /// <summary>"1st" / "22nd" / "--"。</summary>
        public static string Of(int rank)
        {
            if (rank < 1 || rank > MaxRank)
            {
                return Unknown;
            }

            return Table[rank];
        }
    }
}

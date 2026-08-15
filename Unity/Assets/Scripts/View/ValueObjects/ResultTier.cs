// 仕様書: Unity/docs/.sdd/value-objects/10-result-tier.md
// リザルト演出の分岐を、たった1つの純関数に閉じ込める。

namespace Takoda99.View.ValueObjects
{
    /// <summary>リザルト演出の段階。分岐の基準は FinalRank ただ1つ。</summary>
    public enum ResultTier
    {
        /// <summary>1位。チャンピオン専用の特別演出（最も豪華に）。</summary>
        Champion,

        /// <summary>2〜3位。表彰台の演出。</summary>
        Podium,

        /// <summary>4〜10位。決勝進出者としての演出。</summary>
        Finalist,

        /// <summary>11位以下、および順位不明。通常のリザルト。</summary>
        Standard,
    }

    public static class ResultTierRule
    {
        /// <summary>決勝進出ライン。100秒時点の生存数が10人（本選企画書 3.6）。</summary>
        private const int FinalistRank = 10;

        /// <summary>表彰台。</summary>
        private const int PodiumRank = 3;

        /// <summary>
        /// 最終順位から演出の段階を決める。**分岐の基準はこの値だけ**
        /// （途中の StoreEliminatedBatch を使わない）。
        /// finalRank が 0 以下（PersonalResult 未受信）なら Standard。
        /// </summary>
        /// <remarks>
        /// 境界はハードコードでよい。<c>GameParametersPublicSubset.CullSchedule</c> から導出しないのは、
        /// 演出の段階が企画の決めた固定値であり、スケジュールが調整されても4段階の意味が変わらないため
        /// （調整対象は中間ステージの <c>targetAliveCount</c> だけで、決勝人数10は動かさない側）。
        /// </remarks>
        public static ResultTier From(int finalRank)
        {
            // 0位は存在しない。PersonalResult 未受信も Standard 側へ倒す。
            if (finalRank <= 0)
            {
                return ResultTier.Standard;
            }

            if (finalRank == 1)
            {
                return ResultTier.Champion;
            }

            if (finalRank <= PodiumRank)
            {
                return ResultTier.Podium;
            }

            return finalRank <= FinalistRank ? ResultTier.Finalist : ResultTier.Standard;
        }
    }
}

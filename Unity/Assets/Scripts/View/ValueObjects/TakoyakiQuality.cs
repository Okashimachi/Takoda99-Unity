// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md §4.3
// 1注文ぶんの打鍵ミス率から、舟皿の盛り付けの出来を決める純関数。
//
// 出来は**見た目だけ**に使う。評価・スコアはサーバー権威であり、ここでは一切算出しない。

namespace Takoda99.View.ValueObjects
{
    /// <summary>盛り付けの出来。数値が大きいほど汚い。</summary>
    public enum TakoyakiQuality
    {
        /// <summary>ミスがほとんど無い。完璧な盛り付け。</summary>
        Clean = 0,

        /// <summary>そこそこミスした。ふつうの盛り付け。</summary>
        Normal = 1,

        /// <summary>ミスが多かった。汚い盛り付け。</summary>
        Dirty = 2,
    }

    /// <summary>打鍵ミス率 → <see cref="TakoyakiQuality"/>。しきい値は CookingAnimationSettings が持つ。</summary>
    public static class TakoyakiQualityRule
    {
        /// <summary>
        /// 1注文ぶんの正打数・ミス数から出来を決める。
        /// **回数ではなく率で見る**：注文個数も単語の長さもサーバーが決めるため、
        /// 回数のしきい値だと注文ごとに難易度が変わってしまう。
        /// 打鍵が1つも無い場合は <see cref="TakoyakiQuality.Clean"/>（ミスしていないため）。
        /// </summary>
        public static TakoyakiQuality From(int correctCount, int missCount, float cleanMaxRatio, float normalMaxRatio)
        {
            var total = correctCount + missCount;
            if (total <= 0)
            {
                return TakoyakiQuality.Clean;
            }

            var ratio = (float)missCount / total;

            if (ratio <= cleanMaxRatio)
            {
                return TakoyakiQuality.Clean;
            }

            return ratio <= normalMaxRatio ? TakoyakiQuality.Normal : TakoyakiQuality.Dirty;
        }
    }
}

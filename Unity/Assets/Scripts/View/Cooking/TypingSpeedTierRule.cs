// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md §4.1
// 打鍵速度（KPM）から「生地を流す穴の数」を引く純関数。
//
// これは見た目の段階決定であって経営ロジックではない（評価・信用・順位に一切影響しない）。
// 算出結果をサーバーへ送ることもない。
//
// **View/ValueObjects/ には置かない。** CookingAnimationSettings.SpeedTier（UnityEngine 依存の
// ScriptableObject の入れ子型）を引数に取るため、UnityEngine 非依存を前提にした
// tests/Takoda99.View.Tests・Takoda99.View.LangVersionCheck の glob 対象（ValueObjects/*.cs）に
// 混ぜるとビルドが壊れる。

using Takoda99.View.ValueObjects;

namespace Takoda99.View.Cooking
{
    /// <summary>打鍵速度（KPM）→ 使う穴数。しきい値の表は CookingAnimationSettings が持つ。</summary>
    public static class TypingSpeedTierRule
    {
        /// <summary>
        /// KPM に対応する段階の index を返す。表が空、または KPM がどの段階にも満たない場合は 0。
        /// 表は minKpm の昇順に並んでいる前提で、条件を満たす最後の要素を採る。
        /// </summary>
        public static int ResolveTierIndex(CookingAnimationSettings.SpeedTier[] tiers, float kpm)
        {
            if (tiers == null || tiers.Length == 0)
            {
                return 0;
            }

            var index = 0;
            for (var i = 0; i < tiers.Length; i++)
            {
                if (kpm >= tiers[i].MinKpm)
                {
                    index = i;
                }
            }

            return index;
        }

        /// <summary>
        /// 段階 index に対応する穴数を返す。表が空のときは <paramref name="fallbackSlotCount"/>。
        /// 返り値は 0..<see cref="TakoyakiStandState.StandCapacity"/> にクランプする。
        /// </summary>
        public static int ResolveSlotCount(
            CookingAnimationSettings.SpeedTier[] tiers,
            int tierIndex,
            int fallbackSlotCount)
        {
            if (tiers == null || tiers.Length == 0)
            {
                return Clamp(fallbackSlotCount);
            }

            if (tierIndex < 0)
            {
                tierIndex = 0;
            }
            else if (tierIndex >= tiers.Length)
            {
                tierIndex = tiers.Length - 1;
            }

            return Clamp(tiers[tierIndex].SlotCount);
        }

        private static int Clamp(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > TakoyakiStandState.StandCapacity ? TakoyakiStandState.StandCapacity : value;
        }
    }
}

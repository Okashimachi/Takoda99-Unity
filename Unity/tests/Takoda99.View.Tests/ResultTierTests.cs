// 仕様書: Unity/docs/.sdd/value-objects/10-result-tier.md §5 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class ResultTierTests
    {
        [Theory]
        [InlineData(-1, ResultTier.Standard)]
        [InlineData(0, ResultTier.Standard)]
        [InlineData(1, ResultTier.Champion)]
        [InlineData(2, ResultTier.Podium)]
        [InlineData(3, ResultTier.Podium)]
        [InlineData(4, ResultTier.Finalist)]
        [InlineData(7, ResultTier.Finalist)]
        [InlineData(10, ResultTier.Finalist)]
        [InlineData(11, ResultTier.Standard)]
        [InlineData(50, ResultTier.Standard)]
        [InlineData(99, ResultTier.Standard)]
        public void 最終順位から演出の段階が決まる(int finalRank, ResultTier expected)
        {
            Assert.Equal(expected, ResultTierRule.From(finalRank));
        }

        /// <summary>範囲外でも例外を投げない（99店を超える値が届いても画面を止めない）。</summary>
        [Fact]
        public void 範囲外の順位でも例外を投げずStandardになる()
        {
            Assert.Equal(ResultTier.Standard, ResultTierRule.From(100));
            Assert.Equal(ResultTier.Standard, ResultTierRule.From(int.MaxValue));
            Assert.Equal(ResultTier.Standard, ResultTierRule.From(int.MinValue));
        }
    }
}

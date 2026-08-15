// 仕様書: Unity/docs/.sdd/hud/02-order-word-emphasis.md §3.1 / §6 観点1

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class OrderWordSizeTierTests
    {
        [Theory]
        [InlineData("た", OrderWordSizeTier.Large)]
        [InlineData("たこ", OrderWordSizeTier.Large)]
        [InlineData("たこや", OrderWordSizeTier.Large)]
        [InlineData("たこやき", OrderWordSizeTier.Medium)]
        [InlineData("たこやきこ", OrderWordSizeTier.Medium)]
        [InlineData("たこやきこな", OrderWordSizeTier.Medium)]
        [InlineData("たこやきこなも", OrderWordSizeTier.Small)]
        [InlineData("あいうえおかきくけこさしすせそ", OrderWordSizeTier.Small)]
        public void 単語長からサイズ段階が決まる(string word, OrderWordSizeTier expected)
        {
            Assert.Equal(expected, OrderWordSizeRule.From(word));
        }

        /// <summary>お題が無い（客の入れ替わりの隙間）ときは枠だけが出る。例外を出さない。</summary>
        [Fact]
        public void 空文字とnullはLargeになる()
        {
            Assert.Equal(OrderWordSizeTier.Large, OrderWordSizeRule.From(""));
            Assert.Equal(OrderWordSizeTier.Large, OrderWordSizeRule.From(null));
        }

        /// <summary>同じ長さの単語は常に同じ段階になる（単語が変わるたびにサイズが動かない）。</summary>
        [Fact]
        public void 同じ長さの単語は常に同じ段階になる()
        {
            Assert.Equal(OrderWordSizeRule.From("たこやき"), OrderWordSizeRule.From("やきそば"));
            Assert.Equal(OrderWordSizeRule.From("たこ"), OrderWordSizeRule.From("いか"));
        }

        [Fact]
        public void 閾値を指定できる()
        {
            // L を 1 文字までに絞ると、2 文字は M へ落ちる。
            Assert.Equal(OrderWordSizeTier.Medium, OrderWordSizeRule.From("たこ", 1, 4));
            Assert.Equal(OrderWordSizeTier.Large, OrderWordSizeRule.From("た", 1, 4));
            Assert.Equal(OrderWordSizeTier.Small, OrderWordSizeRule.From("たこやきこ", 1, 4));
        }
    }
}

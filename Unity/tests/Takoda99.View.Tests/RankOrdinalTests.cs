// 仕様書: Unity/docs/.sdd/value-objects/11-rank-ordinal.md §7 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class RankOrdinalTests
    {
        [Theory]
        [InlineData(1, "1st")]
        [InlineData(2, "2nd")]
        [InlineData(3, "3rd")]
        [InlineData(4, "4th")]
        public void 基本の序数(int rank, string expected)
        {
            Assert.Equal(expected, RankOrdinal.Of(rank));
        }

        [Theory]
        [InlineData(11, "11th")]
        [InlineData(12, "12th")]
        [InlineData(13, "13th")]
        public void 十一から十三はthになる(int rank, string expected)
        {
            Assert.Equal(expected, RankOrdinal.Of(rank));
        }

        [Theory]
        [InlineData(21, "21st")]
        [InlineData(22, "22nd")]
        [InlineData(23, "23rd")]
        public void 二十一から二十三はst_nd_rdになる(int rank, string expected)
        {
            Assert.Equal(expected, RankOrdinal.Of(rank));
        }

        [Fact]
        public void 最大値の99位はthになる()
        {
            Assert.Equal("99th", RankOrdinal.Of(99));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(100)]
        public void 範囲外は未確定表記になる(int rank)
        {
            Assert.Equal("--", RankOrdinal.Of(rank));
        }

        /// <summary>表から引いており毎回生成していないことの確認。</summary>
        [Fact]
        public void 同じ順位なら同一のインスタンスが返る()
        {
            var a = RankOrdinal.Of(5);
            var b = RankOrdinal.Of(5);

            Assert.Same(a, b);
        }
    }
}

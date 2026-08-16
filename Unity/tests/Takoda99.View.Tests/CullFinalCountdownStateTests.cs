// 仕様書: Unity/docs/.sdd/value-objects/14-cull-final-countdown-state.md

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class CullFinalCountdownStateTests
    {
        [Fact]
        public void 段階がNoneなら出さない()
        {
            var state = CullFinalCountdownState.From(CullAlertTier.None, 3_000);

            Assert.False(state.Visible);
            Assert.Equal(0, state.Seconds);
            Assert.Equal(string.Empty, state.Text);
        }

        [Fact]
        public void 残りが窓より多い間は出さない()
        {
            var state = CullFinalCountdownState.From(CullAlertTier.Danger, 5_001);

            Assert.False(state.Visible);
        }

        [Fact]
        public void 窓に入った瞬間から出す()
        {
            var state = CullFinalCountdownState.From(CullAlertTier.Danger, 5_000);

            Assert.True(state.Visible);
            Assert.Equal(5, state.Seconds);
            Assert.Equal("5", state.Text);
        }

        [Fact]
        public void ぎりぎり圏外にも出す()
        {
            var state = CullFinalCountdownState.From(CullAlertTier.Caution, 4_200);

            Assert.True(state.Visible);
            Assert.Equal(5, state.Seconds);
        }

        [Fact]
        public void 残り0では消す()
        {
            // 0 を出したまま残さない。淘汰の瞬間は結果側の演出へ譲る。
            Assert.False(CullFinalCountdownState.From(CullAlertTier.Danger, 0).Visible);
            Assert.False(CullFinalCountdownState.From(CullAlertTier.Danger, -1).Visible);
        }

        [Theory]
        [InlineData(4_001, 5)]
        [InlineData(4_000, 4)]
        [InlineData(1, 1)]
        [InlineData(1_000, 1)]
        [InlineData(1_001, 2)]
        public void 秒は切り上げる(long remainingMs, int expected)
        {
            Assert.Equal(expected, CullFinalCountdownState.From(CullAlertTier.Danger, remainingMs).Seconds);
        }

        [Theory]
        [InlineData(5_000, 0f)]      // 5 が出た瞬間
        [InlineData(4_800, 0.2f)]    // 5 が出て 200ms
        [InlineData(4_001, 0.999f)]  // 5 の終わりぎわ
        [InlineData(4_000, 0f)]      // 4 が出た瞬間
        public void 数字ごとの進み具合を出す(long remainingMs, float expected)
        {
            var state = CullFinalCountdownState.From(CullAlertTier.Danger, remainingMs);

            Assert.Equal(expected, state.SecondProgress, 3);
        }

        [Fact]
        public void 同じ秒なら進み具合が違っても等しい()
        {
            // 文字列の差し替え要否だけを見る。アニメーションは毎フレーム別途適用する。
            var a = CullFinalCountdownState.From(CullAlertTier.Danger, 4_900);
            var b = CullFinalCountdownState.From(CullAlertTier.Danger, 4_100);

            Assert.Equal(a, b);
        }

        [Fact]
        public void 秒が変われば等しくない()
        {
            var a = CullFinalCountdownState.From(CullAlertTier.Danger, 4_100);
            var b = CullFinalCountdownState.From(CullAlertTier.Danger, 3_900);

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void 窓を0以下にすると出さない()
        {
            Assert.False(CullFinalCountdownState.From(CullAlertTier.Danger, 1_000, 0).Visible);
        }
    }
}

// 仕様書: Unity/docs/.sdd/value-objects/07-patience-gauge-state.md §6 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class PatienceGaugeStateTests
    {
        private const long TotalMs = 10_000;

        private static PatienceGaugeState Gauge(long remainingMs) =>
            PatienceGaugeState.From(remainingMs, TotalMs, PatienceGaugeThresholds.Default);

        [Theory]
        [InlineData(10_000, PatienceGaugeStage.Safe)]    // 残 100%
        [InlineData(5_000, PatienceGaugeStage.Safe)]     // 残 50%（閾値ちょうどは Safe）
        [InlineData(4_999, PatienceGaugeStage.Caution)]
        [InlineData(2_500, PatienceGaugeStage.Caution)]  // 残 25%（閾値ちょうどは Caution）
        [InlineData(2_499, PatienceGaugeStage.Danger)]
        [InlineData(0, PatienceGaugeStage.Danger)]
        public void 境界値はいずれも余裕がある側の段階になる(long remainingMs, PatienceGaugeStage expected)
        {
            Assert.Equal(expected, Gauge(remainingMs).Stage);
        }

        [Theory]
        [InlineData(-5_000, 0d)]
        [InlineData(0, 0d)]
        [InlineData(7_500, 0.75d)]
        [InlineData(10_000, 1d)]
        [InlineData(30_000, 1d)]
        public void 残量比は0から1へクランプされる(long remainingMs, double expected)
        {
            Assert.Equal(expected, Gauge(remainingMs).RemainingRatio, 6);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2_500)]
        [InlineData(10_000)]
        public void 左端と残量の和は常に1で右端が動かない(long remainingMs)
        {
            var state = Gauge(remainingMs);

            Assert.Equal(1d, state.LeftEdgeAnchorX + state.RemainingRatio, 6);
        }

        [Fact]
        public void 満タンなら左端はバーの左いっぱいで空なら右端に重なる()
        {
            Assert.Equal(0d, Gauge(TotalMs).LeftEdgeAnchorX, 6);
            Assert.Equal(1d, Gauge(0).LeftEdgeAnchorX, 6);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void 総量が0以下でも0除算せずDangerになる(long totalMs)
        {
            var state = PatienceGaugeState.From(1_000, totalMs, PatienceGaugeThresholds.Default);

            Assert.Equal(PatienceGaugeStage.Danger, state.Stage);
            Assert.Equal(0d, state.RemainingRatio, 6);
        }

        [Fact]
        public void 閾値を差し替えても段階の数は3のまま()
        {
            var thresholds = new PatienceGaugeThresholds(Caution: 0.8d, Danger: 0.6d);

            Assert.Equal(PatienceGaugeStage.Safe, PatienceGaugeState.StageOf(0.8d, thresholds));
            Assert.Equal(PatienceGaugeStage.Caution, PatienceGaugeState.StageOf(0.6d, thresholds));
            Assert.Equal(PatienceGaugeStage.Danger, PatienceGaugeState.StageOf(0.59d, thresholds));
        }
    }
}

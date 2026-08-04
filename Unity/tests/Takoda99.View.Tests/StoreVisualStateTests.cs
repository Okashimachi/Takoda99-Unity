// 仕様書: Unity/docs/.sdd/value-objects/01-store-visual-state.md §6 テスト観点

using System.Collections.Generic;
using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class StoreVisualStateTests
    {
        private static readonly StoreEvalThresholds Thresholds = new StoreEvalThresholds(High: 0.7, Mid: 0.3);

        [Theory]
        [InlineData(1.0, StoreEvalLevel.High)]
        [InlineData(0.7, StoreEvalLevel.High)]   // 閾値ちょうどは High（>= の向き）
        [InlineData(0.69, StoreEvalLevel.Mid)]
        [InlineData(0.3, StoreEvalLevel.Mid)]    // 閾値ちょうどは Mid
        [InlineData(0.29, StoreEvalLevel.Low)]
        [InlineData(0.0, StoreEvalLevel.Low)]
        public void 境界値はいずれも上位側の区分になる(double evalNormalized, StoreEvalLevel expected)
        {
            var state = StoreVisualState.From("store-01", evalNormalized, alive: true, Thresholds);

            Assert.Equal(expected, state.EvalLevel);
            Assert.False(state.Eliminated);
        }

        [Fact]
        public void Aliveがfalseになった瞬間にEliminatedがtrueになる()
        {
            var alive = StoreVisualState.From("store-01", 0.9, alive: true, Thresholds);
            var eliminated = StoreVisualState.From("store-01", 0.9, alive: false, Thresholds, alive);

            Assert.False(alive.Eliminated);
            Assert.True(eliminated.Eliminated);
        }

        [Fact]
        public void 脱落後は評価が更新されてもEvalLevelが凍結される()
        {
            var lastAlive = StoreVisualState.From("store-01", 0.9, alive: true, Thresholds);
            Assert.Equal(StoreEvalLevel.High, lastAlive.EvalLevel);

            // 脱落後に低い評価が届いても、直近生存時点の区分を保持する
            var afterElimination = StoreVisualState.From("store-01", 0.05, alive: false, Thresholds, lastAlive);

            Assert.True(afterElimination.Eliminated);
            Assert.Equal(StoreEvalLevel.High, afterElimination.EvalLevel);
        }

        [Fact]
        public void 直前の状態が無い脱落店は受信値から分類する()
        {
            var state = StoreVisualState.From("store-01", 0.1, alive: false, Thresholds);

            Assert.True(state.Eliminated);
            Assert.Equal(StoreEvalLevel.Low, state.EvalLevel);
        }

        [Fact]
        public void 全店ぶんの変換で脱落店だけが凍結される()
        {
            var stores = new List<StoreVisualSource>
            {
                new StoreVisualSource("store-01", 0.9, true),
                new StoreVisualSource("store-02", 0.5, true),
                new StoreVisualSource("store-03", 0.1, true),
            };
            var first = StoreVisualState.FromAll(stores, Thresholds);
            var previous = new Dictionary<string, StoreVisualState>();
            foreach (var state in first)
            {
                previous[state.StoreId] = state;
            }

            var next = StoreVisualState.FromAll(
                new List<StoreVisualSource>
                {
                    new StoreVisualSource("store-01", 0.0, false), // 脱落：High のまま凍結
                    new StoreVisualSource("store-02", 0.9, true),  // 生存：再計算される
                    new StoreVisualSource("store-03", 0.1, true),
                },
                Thresholds,
                previous);

            Assert.Equal(StoreEvalLevel.High, next[0].EvalLevel);
            Assert.True(next[0].Eliminated);
            Assert.Equal(StoreEvalLevel.High, next[1].EvalLevel);
            Assert.False(next[1].Eliminated);
            Assert.Equal(StoreEvalLevel.Low, next[2].EvalLevel);
        }

        [Fact]
        public void 仮置きの既定閾値は上位下位を3分割する()
        {
            var defaults = StoreEvalThresholds.Default;

            Assert.Equal(StoreEvalLevel.High, StoreVisualState.From("s", 0.8, true, defaults).EvalLevel);
            Assert.Equal(StoreEvalLevel.Mid, StoreVisualState.From("s", 0.5, true, defaults).EvalLevel);
            Assert.Equal(StoreEvalLevel.Low, StoreVisualState.From("s", 0.2, true, defaults).EvalLevel);
        }
    }
}

// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md §7 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class RankBarViewStateTests
    {
        [Fact]
        public void EvalNormalizedの更新がSelfPositionRatioに反映される()
        {
            var state = RankBarViewState.From(evalNormalized: 0.25, aliveCount: 60, maxStores: 99);
            Assert.Equal(0.25f, state.SelfPositionRatio, 5);

            var updated = RankBarViewState.From(evalNormalized: 0.9, aliveCount: 40, maxStores: 99);
            Assert.Equal(0.9f, updated.SelfPositionRatio, 5);
            Assert.Equal(40, updated.AliveCount);
            Assert.Equal(99, updated.MaxStores);
        }

        [Fact]
        public void MaxStoresが0でも0除算しない()
        {
            var state = RankBarViewState.From(evalNormalized: 0.5, aliveCount: 0, maxStores: 0);

            Assert.Equal(0f, state.AliveRatio);
        }

        [Fact]
        public void 生存比率が算出できる()
        {
            var state = RankBarViewState.From(evalNormalized: 0.5, aliveCount: 33, maxStores: 99);

            Assert.Equal(1f / 3f, state.AliveRatio, 5);
        }
    }
}

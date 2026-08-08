// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md §7 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class RankBarViewStateTests
    {
        [Fact]
        public void Rankの更新がSelfPositionRatioに反映される()
        {
            var state = RankBarViewState.From(rank: 1, aliveCount: 60, maxStores: 99, stormThresholdPct: 0.2);
            Assert.Equal(1f, state.SelfPositionRatio, 5);

            var updated = RankBarViewState.From(rank: 99, aliveCount: 40, maxStores: 99, stormThresholdPct: 0.2);
            Assert.Equal(0f, updated.SelfPositionRatio, 5);
            Assert.Equal(40, updated.AliveCount);
            Assert.Equal(99, updated.MaxStores);
        }

        [Fact]
        public void Rank未受信は最下位側に置く()
        {
            // 試合開始直後は EvaluationUpdate がまだ届かず Rank = 0。
            // ここを 1位（右端）に丸めると、開始と同時にマーカーが1位の位置へ立つ。
            var state = RankBarViewState.From(rank: 0, aliveCount: 99, maxStores: 99, stormThresholdPct: 0.2);

            Assert.Equal(0f, state.SelfPositionRatio, 5);
        }

        [Fact]
        public void SelfPositionRatioはMaxStoresを固定軸として算出する()
        {
            var state = RankBarViewState.From(rank: 25, aliveCount: 99, maxStores: 99, stormThresholdPct: 0.2);

            Assert.Equal(74f / 98f, state.SelfPositionRatio, 5);
        }

        [Fact]
        public void AliveBoundaryRatioは生存最下位の順位から算出する()
        {
            var state = RankBarViewState.From(rank: 1, aliveCount: 60, maxStores: 99, stormThresholdPct: 0.2);

            Assert.Equal(39f / 98f, state.AliveBoundaryRatio, 5);
        }

        [Fact]
        public void 脱落が進むほどAliveBoundaryRatioが右へ寄る()
        {
            var early = RankBarViewState.From(rank: 1, aliveCount: 60, maxStores: 99, stormThresholdPct: 0.2);
            var late = RankBarViewState.From(rank: 1, aliveCount: 40, maxStores: 99, stormThresholdPct: 0.2);

            Assert.True(late.AliveBoundaryRatio > early.AliveBoundaryRatio);
        }

        [Fact]
        public void CullCountはAliveCountとStormThresholdPctからceilで算出する()
        {
            var state = RankBarViewState.From(rank: 1, aliveCount: 60, maxStores: 99, stormThresholdPct: 0.2);
            Assert.Equal(12, state.CullCount);

            var rounded = RankBarViewState.From(rank: 1, aliveCount: 33, maxStores: 99, stormThresholdPct: 0.1);
            Assert.Equal(4, rounded.CullCount);
        }

        [Fact]
        public void DangerBoundaryRatioは下位淘汰対象の最上位の順位から算出する()
        {
            var state = RankBarViewState.From(rank: 1, aliveCount: 60, maxStores: 99, stormThresholdPct: 0.2);

            // CullCount=12 → 境界順位=60-12+1=49 → (99-49)/98
            Assert.Equal(50f / 98f, state.DangerBoundaryRatio, 5);
        }

        [Fact]
        public void MaxStoresが1以下でも0除算しない()
        {
            var state = RankBarViewState.From(rank: 5, aliveCount: 0, maxStores: 0, stormThresholdPct: 0.2);

            Assert.Equal(0f, state.SelfPositionRatio);
            Assert.Equal(0f, state.AliveBoundaryRatio);
            Assert.Equal(0f, state.DangerBoundaryRatio);
            Assert.Equal(0f, state.AliveRatio);
        }

        [Fact]
        public void 生存比率が算出できる()
        {
            var state = RankBarViewState.From(rank: 1, aliveCount: 33, maxStores: 99, stormThresholdPct: 0.2);

            Assert.Equal(1f / 3f, state.AliveRatio, 5);
        }

        [Fact]
        public void StormThresholdPctがそのまま反映される()
        {
            var state = RankBarViewState.From(rank: 1, aliveCount: 50, maxStores: 99, stormThresholdPct: 0.2);

            Assert.Equal(0.2f, state.StormThresholdPct, 5);
        }
    }
}

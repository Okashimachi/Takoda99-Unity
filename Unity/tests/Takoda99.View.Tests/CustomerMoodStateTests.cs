// 仕様書: Unity/docs/.sdd/value-objects/02-customer-mood-state.md §6 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class CustomerMoodStateTests
    {
        private static readonly CustomerMoodThresholds Thresholds =
            new CustomerMoodThresholds(Irritated: 0.6, Angry: 0.3);

        private static CustomerMoodState Mood(long matchElapsedMs, int patienceMaxMs = 10_000) =>
            CustomerMoodState.From("c1", patienceMaxMs, arrivedAtElapsedMs: 0, matchElapsedMs, Thresholds);

        [Theory]
        [InlineData(0, CustomerMood.Calm)]        // ratio 1.0
        [InlineData(4_000, CustomerMood.Calm)]    // ratio 0.6（閾値ちょうどは Calm）
        [InlineData(4_100, CustomerMood.Irritated)]
        [InlineData(7_000, CustomerMood.Irritated)] // ratio 0.3（閾値ちょうどは Irritated）
        [InlineData(7_100, CustomerMood.Angry)]
        [InlineData(9_999, CustomerMood.Angry)]
        public void 境界値はいずれも上位側の区分になる(long elapsedMs, CustomerMood expected)
        {
            Assert.Equal(expected, Mood(elapsedMs).Mood);
        }

        [Fact]
        public void 残量推定が0に到達した瞬間にTurnedAwayへ切り替わる()
        {
            Assert.Equal(CustomerMood.Angry, Mood(9_999).Mood);
            Assert.Equal(CustomerMood.TurnedAway, Mood(10_000).Mood);
            Assert.Equal(CustomerMood.TurnedAway, Mood(30_000).Mood);
        }

        [Fact]
        public void 残量推定は来店時点の経過時間を起点に減る()
        {
            var leftMs = CustomerMoodState.PatienceLeftMsDisplay(
                patienceMaxMs: 10_000, arrivedAtElapsedMs: 20_000, matchElapsedMs: 23_000);

            Assert.Equal(7_000, leftMs);
        }

        [Fact]
        public void 来店直後の客はCalm()
        {
            var state = CustomerMoodState.From("c1", 10_000, arrivedAtElapsedMs: 20_000,
                matchElapsedMs: 20_000, Thresholds);

            Assert.Equal(CustomerMood.Calm, state.Mood);
            Assert.Equal("c1", state.CustomerId);
        }

        [Fact]
        public void PatienceMaxMsが0以下でも0除算しない()
        {
            var state = CustomerMoodState.From("c1", 0, 0, 0, Thresholds);

            Assert.Equal(CustomerMood.TurnedAway, state.Mood);
        }
    }
}

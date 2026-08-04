// 仕様書: Unity/docs/.sdd/value-objects/04-credit-life-lantern-state.md §6 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class CreditLifeLanternStateTests
    {
        [Fact]
        public void CreditLifeがinitialLifeのとき全点灯()
        {
            var state = CreditLifeLanternState.From(creditLife: 3, initialLife: 3);

            Assert.Equal(3, state.Lanterns.Count);
            foreach (var lantern in state.Lanterns)
            {
                Assert.Equal(LanternState.Lit, lantern);
            }
        }

        [Fact]
        public void CreditLifeが0のとき全消灯()
        {
            var state = CreditLifeLanternState.From(creditLife: 0, initialLife: 3);

            foreach (var lantern in state.Lanterns)
            {
                Assert.Equal(LanternState.Unlit, lantern);
            }
        }

        [Fact]
        public void 添字の小さい方から点灯する()
        {
            var state = CreditLifeLanternState.From(creditLife: 1, initialLife: 3);

            Assert.Equal(LanternState.Lit, state.Lanterns[0]);
            Assert.Equal(LanternState.Unlit, state.Lanterns[1]);
            Assert.Equal(LanternState.Unlit, state.Lanterns[2]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void LanternsCountは常にinitialLifeと一致する(int initialLife)
        {
            var state = CreditLifeLanternState.From(creditLife: 1, initialLife: initialLife);

            Assert.Equal(initialLife, state.Lanterns.Count);
        }

        [Fact]
        public void initialLifeが未受信0でも破綻しない()
        {
            var state = CreditLifeLanternState.From(creditLife: 0, initialLife: 0);

            Assert.Empty(state.Lanterns);
        }
    }
}

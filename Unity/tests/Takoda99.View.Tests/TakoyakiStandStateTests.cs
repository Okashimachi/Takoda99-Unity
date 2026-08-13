// 仕様書: Unity/docs/.sdd/value-objects/03-takoyaki-stand-state.md §8 テスト観点
// 本選（v0.8.0）で相対評価が廃止され、生地を流す穴数の供給元が
// 評価3段階 → 注文個数（CustomerView.OrderCount）へ変わったため、期待値を書き換えている。

using System.Linq;
using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class TakoyakiStandStateTests
    {
        private static int CountOf(TakoyakiStandState stand, TakoyakiSlotState state)
            => stand.Slots.Count(s => s == state);

        [Fact]
        public void 注文個数ぶんだけ生地が流される()
        {
            var stand = TakoyakiStandState.From(orderCount: 6, typedWordCount: 0);

            Assert.Equal(6, CountOf(stand, TakoyakiSlotState.Batter));
            Assert.Equal(0, CountOf(stand, TakoyakiSlotState.Cooked));
            Assert.Equal(18, CountOf(stand, TakoyakiSlotState.Empty));
        }

        [Fact]
        public void 打ち終えた語数ぶんが焼き上がりに変わる()
        {
            var stand = TakoyakiStandState.From(orderCount: 6, typedWordCount: 2);

            Assert.Equal(2, CountOf(stand, TakoyakiSlotState.Cooked));
            Assert.Equal(4, CountOf(stand, TakoyakiSlotState.Batter));
            Assert.Equal(18, CountOf(stand, TakoyakiSlotState.Empty));

            // 先頭から順に焼き上がる（行優先・左上原点）。
            Assert.Equal(TakoyakiSlotState.Cooked, stand.Slots[0]);
            Assert.Equal(TakoyakiSlotState.Cooked, stand.Slots[1]);
            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[2]);
        }

        [Fact]
        public void 穴数は常に24を返す()
        {
            Assert.Equal(24, TakoyakiStandState.From(0, 0).Slots.Count);
            Assert.Equal(24, TakoyakiStandState.From(12, 12).Slots.Count);
            Assert.Equal(24, TakoyakiStandState.From(99, 99).Slots.Count);
        }

        /// <summary>注文個数が台の容量を超えても破綻しない。</summary>
        [Fact]
        public void 注文個数が24を超えても24穴でクランプされる()
        {
            var stand = TakoyakiStandState.From(orderCount: 40, typedWordCount: 0);

            Assert.Equal(24, CountOf(stand, TakoyakiSlotState.Batter));
            Assert.Equal(0, CountOf(stand, TakoyakiSlotState.Empty));
        }

        [Fact]
        public void 打ち終えた語数が注文個数を超えてもクランプされる()
        {
            var stand = TakoyakiStandState.From(orderCount: 4, typedWordCount: 30);

            Assert.Equal(4, CountOf(stand, TakoyakiSlotState.Cooked));
            Assert.Equal(0, CountOf(stand, TakoyakiSlotState.Batter));
        }

        [Fact]
        public void 負の語数は0として扱われる()
        {
            var stand = TakoyakiStandState.From(orderCount: 4, typedWordCount: -5);

            Assert.Equal(0, CountOf(stand, TakoyakiSlotState.Cooked));
            Assert.Equal(4, CountOf(stand, TakoyakiSlotState.Batter));
        }

        [Fact]
        public void 負の注文個数は0として扱われる()
        {
            var stand = TakoyakiStandState.From(orderCount: -3, typedWordCount: 0);

            Assert.Equal(24, CountOf(stand, TakoyakiSlotState.Empty));
        }

        /// <summary>対応中の客がいないときは台が空になる。</summary>
        [Fact]
        public void Idleはすべて空になる()
        {
            var idle = TakoyakiStandState.Idle();

            Assert.Equal(24, CountOf(idle, TakoyakiSlotState.Empty));
        }
    }
}

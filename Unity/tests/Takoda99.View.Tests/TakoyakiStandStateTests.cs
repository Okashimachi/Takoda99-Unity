// 仕様書: Unity/docs/.sdd/value-objects/03-takoyaki-stand-state.md §6 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class TakoyakiStandStateTests
    {
        [Fact]
        public void 注文個数が穴数未満なら余った穴はEmpty()
        {
            var stand = TakoyakiStandState.From(orderCount: 4, typedWordCount: 0);

            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[3]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[4]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[TakoyakiStandState.StandCapacity - 1]);
        }

        [Fact]
        public void TypedWordCountの増加でindex順にBatterからCookedへ変わる()
        {
            var stand = TakoyakiStandState.From(orderCount: 4, typedWordCount: 2);

            Assert.Equal(TakoyakiSlotState.Cooked, stand.Slots[0]);
            Assert.Equal(TakoyakiSlotState.Cooked, stand.Slots[1]);
            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[2]);
            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[3]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[4]);
        }

        [Fact]
        public void Slotsの長さは常にStandCapacityと一致する()
        {
            Assert.Equal(24, TakoyakiStandState.StandCapacity);
            Assert.Equal(6, TakoyakiStandState.StandColumns);
            Assert.Equal(4, TakoyakiStandState.StandRows);

            Assert.Equal(24, TakoyakiStandState.From(0, 0).Slots.Count);
            Assert.Equal(24, TakoyakiStandState.From(12, 12).Slots.Count);
            Assert.Equal(24, TakoyakiStandState.From(99, 99).Slots.Count);
        }

        [Fact]
        public void 客の繰り上がりで全穴がEmptyにリセットされてから再構成される()
        {
            var served = TakoyakiStandState.From(orderCount: 4, typedWordCount: 4);
            Assert.Equal(TakoyakiSlotState.Cooked, served.Slots[0]);

            var idle = TakoyakiStandState.Idle;
            foreach (var slot in idle.Slots)
            {
                Assert.Equal(TakoyakiSlotState.Empty, slot);
            }

            var next = TakoyakiStandState.From(orderCount: 6, typedWordCount: 0);
            Assert.Equal(TakoyakiSlotState.Batter, next.Slots[5]);
            Assert.Equal(TakoyakiSlotState.Empty, next.Slots[6]);
        }

        [Fact]
        public void TypedWordCountがOrderCountを超えてもoccupiedCountでクランプされる()
        {
            var stand = TakoyakiStandState.From(orderCount: 4, typedWordCount: 10);

            Assert.Equal(TakoyakiSlotState.Cooked, stand.Slots[3]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[4]);
        }

        [Fact]
        public void OrderCountが穴数を超える場合はStandCapacityでクランプされる()
        {
            var stand = TakoyakiStandState.From(orderCount: 30, typedWordCount: 0);

            foreach (var slot in stand.Slots)
            {
                Assert.Equal(TakoyakiSlotState.Batter, slot);
            }
        }

        [Fact]
        public void 負値が渡されても配列外参照にならない()
        {
            var stand = TakoyakiStandState.From(orderCount: -1, typedWordCount: -5);

            Assert.Equal(24, stand.Slots.Count);
            foreach (var slot in stand.Slots)
            {
                Assert.Equal(TakoyakiSlotState.Empty, slot);
            }
        }
    }
}

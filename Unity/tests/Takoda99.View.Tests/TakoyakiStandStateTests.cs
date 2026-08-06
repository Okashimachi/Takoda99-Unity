// 仕様書: Unity/docs/.sdd/value-objects/03-takoyaki-stand-state.md §6 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class TakoyakiStandStateTests
    {
        [Fact]
        public void EvalLevelがLowなら生地マスは12個で1から2行目まで()
        {
            var stand = TakoyakiStandState.From(StoreEvalLevel.Low, typedWordCount: 0);

            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[11]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[12]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[TakoyakiStandState.StandCapacity - 1]);
        }

        [Fact]
        public void EvalLevelがMidなら生地マスは18個で1から3行目まで()
        {
            var stand = TakoyakiStandState.From(StoreEvalLevel.Mid, typedWordCount: 0);

            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[17]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[18]);
        }

        [Fact]
        public void EvalLevelがHighなら生地マスは全24個()
        {
            var stand = TakoyakiStandState.From(StoreEvalLevel.High, typedWordCount: 0);

            foreach (var slot in stand.Slots)
            {
                Assert.Equal(TakoyakiSlotState.Batter, slot);
            }
        }

        [Fact]
        public void TypedWordCountの増加でindex順にBatterからCookedへ変わる()
        {
            var stand = TakoyakiStandState.From(StoreEvalLevel.Low, typedWordCount: 2);

            Assert.Equal(TakoyakiSlotState.Cooked, stand.Slots[0]);
            Assert.Equal(TakoyakiSlotState.Cooked, stand.Slots[1]);
            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[2]);
            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[11]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[12]);
        }

        [Fact]
        public void Slotsの長さは常にStandCapacityと一致する()
        {
            Assert.Equal(24, TakoyakiStandState.StandCapacity);
            Assert.Equal(6, TakoyakiStandState.StandColumns);
            Assert.Equal(4, TakoyakiStandState.StandRows);

            Assert.Equal(24, TakoyakiStandState.From(StoreEvalLevel.Low, 0).Slots.Count);
            Assert.Equal(24, TakoyakiStandState.From(StoreEvalLevel.Mid, 12).Slots.Count);
            Assert.Equal(24, TakoyakiStandState.From(StoreEvalLevel.High, 99).Slots.Count);
        }

        [Fact]
        public void 客の繰り上がりでCookedがBatterへ戻りEmptyには戻らない()
        {
            var served = TakoyakiStandState.From(StoreEvalLevel.Low, typedWordCount: 4);
            Assert.Equal(TakoyakiSlotState.Cooked, served.Slots[0]);

            var idle = TakoyakiStandState.Idle(StoreEvalLevel.Low);
            Assert.Equal(TakoyakiSlotState.Batter, idle.Slots[0]);
            Assert.Equal(TakoyakiSlotState.Batter, idle.Slots[11]);
            Assert.Equal(TakoyakiSlotState.Empty, idle.Slots[12]);
        }

        [Fact]
        public void TypedWordCountが生地マス数を超えてもoccupiedCountでクランプされる()
        {
            var stand = TakoyakiStandState.From(StoreEvalLevel.Low, typedWordCount: 30);

            for (var i = 0; i < TakoyakiStandState.BatterCountLow; i++)
            {
                Assert.Equal(TakoyakiSlotState.Cooked, stand.Slots[i]);
            }

            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[TakoyakiStandState.BatterCountLow]);
        }

        [Fact]
        public void 負値が渡されても配列外参照にならない()
        {
            var stand = TakoyakiStandState.From(StoreEvalLevel.Low, typedWordCount: -5);

            Assert.Equal(24, stand.Slots.Count);
            Assert.Equal(TakoyakiSlotState.Batter, stand.Slots[0]);
            Assert.Equal(TakoyakiSlotState.Empty, stand.Slots[12]);
        }
    }
}

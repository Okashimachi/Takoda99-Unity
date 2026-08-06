// 仕様書: Unity/docs/.sdd/value-objects/03-takoyaki-stand-state.md
// たこ焼き台（6列×4行＝24穴）の各穴の見た目状態。提供の確定はしない（OrderProgressState 側の責務）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（StoreVisualState.cs 冒頭の注記を参照）。

using System.Collections.Generic;

namespace Takoda99.View.ValueObjects
{
    public enum TakoyakiSlotState { Empty, Batter, Cooked }

    /// <summary>
    /// たこ焼き台の各穴が「なにもない／生地／焼けた」のどれかを表す表示用状態。
    /// グリッド形状をView側に持たせないため、穴数・列数・行数はここで定義する。
    /// </summary>
    public readonly struct TakoyakiStandState
    {
        /// <summary>長さ = StandCapacity(24)。index = row * StandColumns + col（行優先・左上原点）。</summary>
        public IReadOnlyList<TakoyakiSlotState> Slots { get; }

        public TakoyakiStandState(IReadOnlyList<TakoyakiSlotState> Slots)
        {
            this.Slots = Slots;
        }

        public const int StandColumns = 6; // 横
        public const int StandRows = 4;    // 縦
        public const int StandCapacity = StandColumns * StandRows; // 24

        // 生地を流しておく穴の数（評価3段階に対応）。いずれも StandColumns の倍数。
        public const int BatterCountLow = 12;
        public const int BatterCountMid = 18;
        public const int BatterCountHigh = StandCapacity; // 24

        /// <summary>
        /// 評価3段階（<c>StoreEvalLevel</c>）と、いま対応中の客のノルマのうち入力を終えた語数
        /// （<c>OrderProgressState.TypedWordCount</c>）から変換する。
        /// 生地を流す穴数（<c>occupiedCount</c>）は注文個数ではなく評価から決まる（決定ログ D-05）。
        /// </summary>
        public static TakoyakiStandState From(StoreEvalLevel evalLevel, int typedWordCount)
        {
            var occupiedCount = OccupiedCount(evalLevel);
            var cookedCount = Clamp(typedWordCount, 0, occupiedCount);

            var slots = new TakoyakiSlotState[StandCapacity];
            for (var i = 0; i < StandCapacity; i++)
            {
                if (i < cookedCount)
                {
                    slots[i] = TakoyakiSlotState.Cooked; // タイプ完了済み。提供待ち
                }
                else if (i < occupiedCount)
                {
                    slots[i] = TakoyakiSlotState.Batter; // 未クリアだが生地は流してある
                }
                else
                {
                    slots[i] = TakoyakiSlotState.Empty; // 生地を流していない穴
                }
            }

            return new TakoyakiStandState(slots);
        }

        /// <summary>対応中の客がいないときの台（評価に応じた生地マスのみ・すべて未クリア）。</summary>
        public static TakoyakiStandState Idle(StoreEvalLevel evalLevel) => From(evalLevel, 0);

        private static int OccupiedCount(StoreEvalLevel evalLevel)
        {
            switch (evalLevel)
            {
                case StoreEvalLevel.High:
                    return BatterCountHigh;
                case StoreEvalLevel.Mid:
                    return BatterCountMid;
                default:
                    return BatterCountLow;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}

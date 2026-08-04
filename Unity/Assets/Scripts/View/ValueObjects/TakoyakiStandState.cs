// 仕様書: Unity/docs/.sdd/value-objects/03-takoyaki-stand-state.md
// たこ焼き台（6列×4行＝24穴）の各穴の見た目状態。提供の確定はしない（OrderProgressState 側の責務）。

using System.Collections.Generic;

namespace Takoda99.View.ValueObjects
{
    public enum TakoyakiSlotState { Empty, Batter, Cooked }

    /// <summary>
    /// たこ焼き台の各穴が「なにもない／生地／焼けた」のどれかを表す表示用状態。
    /// グリッド形状をView側に持たせないため、穴数・列数・行数はここで定義する。
    /// </summary>
    public readonly record struct TakoyakiStandState(
        IReadOnlyList<TakoyakiSlotState> Slots // 長さ = StandCapacity(24)。index = row * StandColumns + col
    )
    {
        public const int StandColumns = 6; // 横
        public const int StandRows = 4;    // 縦
        public const int StandCapacity = StandColumns * StandRows; // 24

        /// <summary>
        /// <c>OrderProgressState</c> の <c>OrderCount</c> / <c>TypedWordCount</c> から変換する。
        /// pureC# 側の型を Unity から参照する方法が未確定のため、入力は素の値で受ける。
        /// </summary>
        public static TakoyakiStandState From(int orderCount, int typedWordCount)
        {
            // 注文個数と台の穴数の小さい方が「生地を流してある穴」。
            // 用語集4章の注文個数(4/6/8/12)は 24 未満のため現行パラメータでは min は発動しないが、
            // OrderCount が 24 を超え得るようになった場合に備えた防御的なクランプとして残す（SV-14）。
            var occupiedCount = Clamp(orderCount, 0, StandCapacity);
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
                    slots[i] = TakoyakiSlotState.Empty; // 対応する注文がない穴
                }
            }

            return new TakoyakiStandState(slots);
        }

        /// <summary>対応中の客がいないときの台（全穴 <c>Empty</c>）。</summary>
        public static TakoyakiStandState Idle => From(0, 0);

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

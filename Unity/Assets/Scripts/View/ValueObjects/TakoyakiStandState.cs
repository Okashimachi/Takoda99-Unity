// 仕様書: Unity/docs/.sdd/value-objects/03-takoyaki-stand-state.md
// たこ焼き台（6列×4行＝24穴）の各穴の見た目状態。提供の確定はしない（OrderProgressState 側の責務）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（Unity のスクリプティングランタイム制約）。

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

        /// <summary>対応中の客がいないときに生地を流しておく穴の数。</summary>
        public const int IdleBatterCount = 0;

        /// <summary>
        /// いま対応中の客の注文個数（<c>CustomerView.OrderCount</c> ＝ たこ焼きの個数）と、
        /// そのうち入力を終えた語数（<c>OrderProgressState.TypedWordCount</c>）から変換する。
        /// </summary>
        /// <remarks>
        /// **v0.8.0（本選）で入力が変わった。** 予選では生地を流す穴数を評価3段階から決めていたが
        /// （決定ログ D-05）、相対評価そのものが廃止されたため供給元が無くなった。
        /// 代わりに注文個数を使う。注文個数＝たこ焼きの個数なので、
        /// 「台に並んだ生地の数 ＝ この客に出す個数」となり、注文カウンタ <c>x/N</c> と一致する。
        /// </remarks>
        public static TakoyakiStandState From(int orderCount, int typedWordCount)
        {
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
                    slots[i] = TakoyakiSlotState.Empty; // 生地を流していない穴
                }
            }

            return new TakoyakiStandState(slots);
        }

        /// <summary>対応中の客がいないときの台（すべて空）。</summary>
        public static TakoyakiStandState Idle() => From(IdleBatterCount, 0);

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

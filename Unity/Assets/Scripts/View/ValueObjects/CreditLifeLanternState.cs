// 仕様書: Unity/docs/.sdd/value-objects/04-credit-life-lantern-state.md
// 信用ライフ（提灯）の点灯/消灯。増減の判定はサーバー権威で、ここは表示への変換だけを行う。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（StoreVisualState.cs 冒頭の注記を参照）。

using System.Collections.Generic;

namespace Takoda99.View.ValueObjects
{
    public enum LanternState { Lit, Unlit }

    /// <summary>画面左上の提灯（信用ライフ残数）の表示用状態。</summary>
    public readonly struct CreditLifeLanternState
    {
        /// <summary>長さ = initialLife（例:3）。添字の小さい方から点灯。</summary>
        public IReadOnlyList<LanternState> Lanterns { get; }

        public CreditLifeLanternState(IReadOnlyList<LanternState> Lanterns)
        {
            this.Lanterns = Lanterns;
        }

        /// <summary>
        /// <c>StoreState.CreditLife</c> と <c>initialLife</c>（<c>MatchStart.params</c> 由来）から変換する。
        /// 添字の小さい方から点灯とみなす。
        /// </summary>
        public static CreditLifeLanternState From(int creditLife, int initialLife)
        {
            var count = initialLife > 0 ? initialLife : 0;
            var lanterns = new LanternState[count];
            for (var i = 0; i < count; i++)
            {
                lanterns[i] = i < creditLife ? LanternState.Lit : LanternState.Unlit;
            }

            return new CreditLifeLanternState(lanterns);
        }
    }
}

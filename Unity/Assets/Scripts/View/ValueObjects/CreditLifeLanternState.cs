// 仕様書: Unity/docs/.sdd/value-objects/04-credit-life-lantern-state.md
// 信用ライフ（提灯）の点灯/消灯。増減の判定はサーバー権威で、ここは表示への変換だけを行う。

using System.Collections.Generic;

namespace Takoda99.View.ValueObjects
{
    public enum LanternState { Lit, Unlit }

    /// <summary>画面左上の提灯（信用ライフ残数）の表示用状態。</summary>
    public readonly record struct CreditLifeLanternState(
        IReadOnlyList<LanternState> Lanterns // 長さ = initialLife（例:3）
    )
    {
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

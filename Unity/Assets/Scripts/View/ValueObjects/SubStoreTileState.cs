// 仕様書: Unity/docs/.sdd/value-objects/06-sub-store-tile-state.md
// 99店ミニ盤面の1マス（他店1店舗）の見た目区分。脱落の判定・順位の算出はしない。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（StoreVisualState.cs 冒頭の注記を参照）。

namespace Takoda99.View.ValueObjects
{
    public enum SubStoreTileState
    {
        Life3,          // CreditLife >= 3
        Life2,          // CreditLife == 2
        Life1,          // CreditLife == 1
        JustEliminated, // Alive == false になった直後（既定 3.0 秒）。life0 の見た目
        Eliminated,     // 上記の経過後。屋台の見た目を消し、順位を表示する
    }

    /// <summary>
    /// 99店ミニ盤面の1マスの見た目区分を、信用ライフ（<c>CreditLife</c>）と生存状態から導出する。
    /// <c>StoreVisualState</c>（評価3段階）とは別の指標であり、混同しないこと（決定ログ D-06）。
    /// </summary>
    public static class SubStoreTileStateCalculator
    {
        /// <summary>脱落してから <c>Eliminated</c> へ遷移するまでの既定の経過時間（秒）。</summary>
        public const float DefaultEliminationRevealDelaySec = 3.0f;

        /// <summary>
        /// 経過時間の計測は純粋関数ではないため、View 側（<c>SubStoreTileView</c>）が保持する
        /// <c>elapsedSinceEliminatedSec</c> を受け取って区分を返す。
        /// </summary>
        public static SubStoreTileState From(
            int creditLife,
            bool alive,
            float elapsedSinceEliminatedSec,
            float eliminationRevealDelaySec = DefaultEliminationRevealDelaySec)
        {
            if (alive)
            {
                if (creditLife >= 3)
                {
                    return SubStoreTileState.Life3;
                }

                if (creditLife == 2)
                {
                    return SubStoreTileState.Life2;
                }

                if (creditLife == 1)
                {
                    return SubStoreTileState.Life1;
                }

                // CreditLife <= 0 かつ Alive == true。StoreEliminated 受信までの空白を埋める。
                return SubStoreTileState.JustEliminated;
            }

            return elapsedSinceEliminatedSec < eliminationRevealDelaySec
                ? SubStoreTileState.JustEliminated
                : SubStoreTileState.Eliminated;
        }
    }
}

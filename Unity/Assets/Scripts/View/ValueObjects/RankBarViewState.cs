// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md
// 上部順位バーの表示用状態。評価の増減はクライアントで差分計算しない（EvalDeltaDisplayState は上流待ちで保留）。

namespace Takoda99.View.ValueObjects
{
    /// <summary>
    /// 画面上部の順位バー（自店の相対位置▲マーカー・生存数ラベル）の表示用状態。
    /// </summary>
    /// <remarks>
    /// 評価の増減表示（<c>EvalDeltaDisplayState</c>）は、方向を通知する S2C イベントが Proto に未定義のため
    /// <b>保留</b>（仕様書 §4 / SV-06）。クライアント側で <c>EvalNormalized</c> の差分を取る実装は行わない。
    /// </remarks>
    public readonly record struct RankBarViewState(
        float SelfPositionRatio, // 0(最下位側)..1(1位側)
        int AliveCount,
        int MaxStores
    )
    {
        /// <summary>
        /// <c>StoreState.EvalNormalized</c> と <c>MatchState</c> の生存数・最大店舗数から変換する。
        /// <c>EvalNormalized</c> は生存店内パーセンタイル(0..1)のため、追加計算なくバー位置に対応する。
        /// </summary>
        public static RankBarViewState From(double evalNormalized, int aliveCount, int maxStores)
        {
            return new RankBarViewState(
                SelfPositionRatio: (float)evalNormalized,
                AliveCount: aliveCount,
                MaxStores: maxStores);
        }

        /// <summary>
        /// 生存数ラベルを比率で表示する場合の値。<c>MaxStores</c> が 0 のときは 0 を返す（0除算しない）。
        /// </summary>
        public float AliveRatio => MaxStores > 0 ? (float)AliveCount / MaxStores : 0f;
    }
}

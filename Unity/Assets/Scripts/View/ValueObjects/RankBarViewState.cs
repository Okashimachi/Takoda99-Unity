// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md
// 上部順位バーの表示用状態。評価の増減はクライアントで差分計算しない（EvalDeltaDisplayState は上流待ちで保留）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（StoreVisualState.cs 冒頭の注記を参照）。

namespace Takoda99.View.ValueObjects
{
    /// <summary>
    /// 画面上部の順位バー（自店の相対位置▲マーカー・生存数ラベル）の表示用状態。
    /// </summary>
    /// <remarks>
    /// 評価の増減表示（<c>EvalDeltaDisplayState</c>）は、方向を通知する S2C イベントが Proto に未定義のため
    /// <b>保留</b>（仕様書 §4 / SV-06）。クライアント側で <c>EvalNormalized</c> の差分を取る実装は行わない。
    /// </remarks>
    public readonly struct RankBarViewState
    {
        /// <summary>0(最下位側)..1(1位側)。</summary>
        public float SelfPositionRatio { get; }

        public int AliveCount { get; }

        public int MaxStores { get; }

        /// <summary>淘汰圏の帯を描く位置（表示専用）。危険判定には使わない（仕様書 §3「使い分け」）。</summary>
        public float StormThresholdPct { get; }

        // SelfAtRisk（仕様書 §2）は pureC# 側の ForcedEliminationWarning.selfAtRisk がまだ
        // Dispatcher/Reducer を通っていないため、配信されるようになるまで追加しない。

        public RankBarViewState(float SelfPositionRatio, int AliveCount, int MaxStores, float StormThresholdPct)
        {
            this.SelfPositionRatio = SelfPositionRatio;
            this.AliveCount = AliveCount;
            this.MaxStores = MaxStores;
            this.StormThresholdPct = StormThresholdPct;
        }

        /// <summary>
        /// <c>StoreState.EvalNormalized</c> と <c>MatchState</c> の生存数・最大店舗数・淘汰閾値から変換する。
        /// <c>EvalNormalized</c> は生存店内パーセンタイル(0..1)のため、追加計算なくバー位置に対応する。
        /// </summary>
        public static RankBarViewState From(double evalNormalized, int aliveCount, int maxStores, double stormThresholdPct)
        {
            return new RankBarViewState(
                SelfPositionRatio: (float)evalNormalized,
                AliveCount: aliveCount,
                MaxStores: maxStores,
                StormThresholdPct: (float)stormThresholdPct);
        }

        /// <summary>
        /// 生存数ラベルを比率で表示する場合の値。<c>MaxStores</c> が 0 のときは 0 を返す（0除算しない）。
        /// </summary>
        public float AliveRatio => MaxStores > 0 ? (float)AliveCount / MaxStores : 0f;
    }
}

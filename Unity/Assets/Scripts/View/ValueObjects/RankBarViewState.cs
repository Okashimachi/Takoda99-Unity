// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md
// 上部順位バーの表示用状態。評価の増減はクライアントで差分計算しない（EvalDeltaDisplayState は上流待ちで保留）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（StoreVisualState.cs 冒頭の注記を参照）。

using System;

namespace Takoda99.View.ValueObjects
{
    /// <summary>
    /// 画面上部の順位バー（自店の相対位置▲マーカー・生存数ラベル・下位淘汰の帯）の表示用状態。
    /// </summary>
    /// <remarks>
    /// バーの横軸は<c>MaxStores</c>（99店）を固定スケールとした順位軸で、右端が1位・左端がMaxStores位。
    /// 脱落店が増えるほど「生存している最下位の順位」の位置（<see cref="AliveBoundaryRatio"/>）が
    /// 右へ寄っていく（[docs/server-sync/01-プロトコル契約の差分.md#sv-05]）。
    /// 評価の増減表示（<c>EvalDeltaDisplayState</c>）は、方向を通知する S2C イベントが Proto に未定義のため
    /// <b>保留</b>（仕様書 §4 / SV-06）。クライアント側で <c>EvalNormalized</c> の差分を取る実装は行わない。
    /// </remarks>
    public readonly struct RankBarViewState
    {
        /// <summary>自店の順位（1..MaxStores、99店全体の固定軸。EvaluationUpdate.Rank）。</summary>
        public int Rank { get; }

        public int AliveCount { get; }

        public int MaxStores { get; }

        /// <summary>下位淘汰で刈られる生存店の割合（表示専用）。危険判定には使わない（仕様書 §3「使い分け」）。</summary>
        public float StormThresholdPct { get; }

        // SelfAtRisk（仕様書 §2）は pureC# 側の ForcedEliminationWarning.selfAtRisk がまだ
        // Dispatcher/Reducer を通っていないため、配信されるようになるまで追加しない。

        public RankBarViewState(int Rank, int AliveCount, int MaxStores, float StormThresholdPct)
        {
            this.Rank = Rank;
            this.AliveCount = AliveCount;
            this.MaxStores = MaxStores;
            this.StormThresholdPct = StormThresholdPct;
        }

        /// <summary>
        /// <c>StoreState.Rank</c> と <c>MatchState</c> の生存数・最大店舗数・淘汰閾値から変換する。
        /// </summary>
        public static RankBarViewState From(int rank, int aliveCount, int maxStores, double stormThresholdPct)
        {
            return new RankBarViewState(
                Rank: rank,
                AliveCount: aliveCount,
                MaxStores: maxStores,
                StormThresholdPct: (float)stormThresholdPct);
        }

        /// <summary>
        /// 生存数ラベルを比率で表示する場合の値。<c>MaxStores</c> が 0 のときは 0 を返す（0除算しない）。
        /// </summary>
        public float AliveRatio => MaxStores > 0 ? (float)AliveCount / MaxStores : 0f;

        /// <summary>自店の位置比率。0(MaxStores位側)..1(1位側)。▲マーカーの位置に使う。</summary>
        public float SelfPositionRatio => PositionRatioOf(Rank);

        /// <summary>
        /// 「生存している最下位の順位」の位置比率。下位淘汰バー（DangerZone）の左端に使う。
        /// 脱落が進むほど右へ寄る。
        /// </summary>
        public float AliveBoundaryRatio => PositionRatioOf(AliveCount);

        /// <summary>
        /// 下位淘汰対象となる生存店のうち最上位（＝最も安全側）の順位の位置比率。SafeZone の左端に使う。
        /// </summary>
        public float DangerBoundaryRatio => PositionRatioOf(AliveCount - CullCount + 1);

        /// <summary>
        /// 次の下位淘汰で刈られる生存店数（<c>ceil(AliveCount * StormThresholdPct)</c>）。表示専用の概算値。
        /// </summary>
        public int CullCount
        {
            get
            {
                if (AliveCount <= 0 || StormThresholdPct <= 0f)
                {
                    return 0;
                }

                // StormThresholdPct は double→float で丸められているため、60*0.2 のような
                // 本来ちょうど割り切れる値がわずかに超過して ceil を1つ多く繰り上げることがある。
                // 表示専用の概算値なので、丸め誤差ぶんの微小イプシロンを引いてから ceil する。
                const double epsilon = 1e-6;
                var raw = (int)Math.Ceiling(AliveCount * (double)StormThresholdPct - epsilon);
                return raw > AliveCount ? AliveCount : raw;
            }
        }

        /// <summary>
        /// 順位 <paramref name="rank"/> を、MaxStores を固定スケールとしたバー上の位置比率(0..1)に変換する。
        /// <c>rank</c> は 1..MaxStores にクランプする。<c>MaxStores</c> が1以下のときは0を返す（0除算しない）。
        /// </summary>
        private float PositionRatioOf(int rank)
        {
            if (MaxStores <= 1)
            {
                return 0f;
            }

            var clampedRank = rank < 1 ? 1 : (rank > MaxStores ? MaxStores : rank);
            return (float)(MaxStores - clampedRank) / (MaxStores - 1);
        }
    }
}

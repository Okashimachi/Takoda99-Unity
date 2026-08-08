// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md
// 星評価（0..5・小数）を、星1つぶんずつの塗り比率へ割る純粋関数。
// 星の値そのものはサーバー権威（EvaluationUpdate.starRating）で、ここでは分配だけを行う。

namespace Takoda99.View.ValueObjects
{
    /// <summary>星評価を星ごとの塗り比率（0..1）へ割る。</summary>
    public static class StarRatingFill
    {
        /// <summary>星の最大数。</summary>
        public const int MaxStars = 5;

        /// <summary>
        /// <paramref name="starRating"/> を先頭の星から順に埋め、
        /// 端数は境目の星1つだけを部分的に塗る比率として返す。
        /// </summary>
        /// <param name="starRating">0..<paramref name="starCount"/> の星評価。範囲外はクランプする。</param>
        /// <param name="starCount">星の個数。既定は <see cref="MaxStars"/>。</param>
        public static float[] From(double starRating, int starCount = MaxStars)
        {
            if (starCount <= 0)
            {
                return new float[0];
            }

            var fills = new float[starCount];
            var remaining = starRating;

            if (remaining < 0d)
            {
                remaining = 0d;
            }
            else if (remaining > starCount)
            {
                remaining = starCount;
            }

            for (var i = 0; i < starCount; i++)
            {
                // 星1つぶんを超えていれば満タン、足りなければその端数だけ塗る。
                var fill = remaining - i;
                if (fill <= 0d)
                {
                    fills[i] = 0f;
                }
                else if (fill >= 1d)
                {
                    fills[i] = 1f;
                }
                else
                {
                    fills[i] = (float)fill;
                }
            }

            return fills;
        }
    }
}

// 1単語を打ち終えたときに、その出来をどのSEで返すかを決める純粋なルール。
//
// 打鍵1回ごとには鳴らさない（1単語につき1回だけ鳴らす）。判定はミス数そのものではなく
// **単語の長さに対するミスの割合**で行う。長い単語で1ミスと、3文字の単語で1ミスを同じ扱いにしない。

using System;

namespace Takoda99.View.ValueObjects
{
    /// <summary>1単語を打ち終えたときの出来。</summary>
    public enum TypingWordOutcome
    {
        /// <summary>ノーミス。</summary>
        Perfect = 0,

        /// <summary>ミスはあったが、単語の長さに対して少ない。</summary>
        Normal = 1,

        /// <summary>単語の長さに対してミスが多い。</summary>
        Missed = 2,
    }

    /// <summary>単語ごとの出来の判定。</summary>
    public static class TypingWordSoundRule
    {
        /// <summary>
        /// 既定のミス率の境目。これ以下なら通常、超えたらミス多発とする
        /// （10打で1ミスまでは通常、2ミス以上でミス多発）。
        /// </summary>
        public const float DefaultMissRatioThreshold = 0.15f;

        /// <summary>
        /// 打ち終えた1単語の出来を決める。
        /// </summary>
        /// <param name="correctCount">その単語で受理された打鍵数。</param>
        /// <param name="missCount">その単語でのミス打鍵数。</param>
        /// <param name="missRatioThreshold">通常とミス多発の境目（ミス数 ÷ 総打鍵数）。</param>
        public static TypingWordOutcome From(int correctCount, int missCount, float missRatioThreshold)
        {
            if (missCount <= 0)
            {
                return TypingWordOutcome.Perfect;
            }

            // 分母は「その単語で叩いた回数」＝正打＋ミス。正打だけを分母にすると、
            // ミスばかりの単語で分母が縮んで率が跳ね上がり、境目の意味が変わってしまう。
            var total = Math.Max(correctCount + missCount, 1);
            var ratio = missCount / (float)total;

            return ratio <= Math.Max(missRatioThreshold, 0f)
                ? TypingWordOutcome.Normal
                : TypingWordOutcome.Missed;
        }
    }
}

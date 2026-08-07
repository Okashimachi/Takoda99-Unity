// 表示名を PlayerName の3テキスト（Left / Middle / Right）へ振り分ける純粋関数。
// 表示名そのものはサーバー権威（MatchmakingJoin で確定した displayName）で、ここでは分割だけを行う。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（StoreVisualState.cs 冒頭の注記を参照）。

namespace Takoda99.View.ValueObjects
{
    /// <summary>表示名を左・中・右の3枠へ割った結果。</summary>
    public readonly struct PlayerNameLayout
    {
        public string Left { get; }

        public string Middle { get; }

        public string Right { get; }

        public PlayerNameLayout(string Left, string Middle, string Right)
        {
            this.Left = Left;
            this.Middle = Middle;
            this.Right = Right;
        }

        /// <summary>屋号として後ろに足す既定の1文字。</summary>
        public const string DefaultSuffix = "屋";

        /// <summary>
        /// 表示名を3枠へ割る。文字数が3の倍数でないときは <paramref name="suffix"/>（既定「屋」）を
        /// 1文字だけ後ろに足してから割り、左右のバランスを取る。
        /// </summary>
        /// <remarks>
        /// 割り方は「余りを真ん中に寄せる（余1）／左と中央に寄せる（余2）」で固定する。
        /// 表示名は6文字固定の想定だが、短い名前でも崩れないよう全長で場合分けする。
        /// <list type="bullet">
        ///   <item>1文字：屋を足さず中央だけに出す（「あ屋」にすると1文字名だけ別物に見えるため）</item>
        ///   <item>2文字：+屋 → 3文字 → 1/1/1</item>
        ///   <item>4文字：+屋 → 5文字 → 2/2/1（「たこ/焼き/屋」）</item>
        ///   <item>5文字：+屋 → 6文字 → 2/2/2</item>
        ///   <item>3の倍数：そのまま均等割り</item>
        /// </list>
        /// </remarks>
        public static PlayerNameLayout From(string displayName, string suffix = DefaultSuffix)
        {
            var name = displayName ?? string.Empty;

            if (name.Length == 0)
            {
                return new PlayerNameLayout(string.Empty, string.Empty, string.Empty);
            }

            // 1文字だけは屋号化しない。「あ」を「あ屋」にすると、6文字名と揃わないうえに
            // 1文字ぶんの枠に収まらなくなる。
            if (name.Length == 1)
            {
                return new PlayerNameLayout(string.Empty, name, string.Empty);
            }

            if (name.Length % 3 != 0)
            {
                name += suffix ?? string.Empty;
            }

            var baseLength = name.Length / 3;
            var remainder = name.Length % 3;

            int leftLength;
            int middleLength;

            if (remainder == 1)
            {
                // 余り1は中央へ。左右が同じ長さになる。
                leftLength = baseLength;
                middleLength = baseLength + 1;
            }
            else if (remainder == 2)
            {
                // 余り2は左と中央へ1文字ずつ。右に「屋」1文字だけが残り、
                // 「たこ/焼き/屋」のように屋号として読める並びになる。
                leftLength = baseLength + 1;
                middleLength = baseLength + 1;
            }
            else
            {
                leftLength = baseLength;
                middleLength = baseLength;
            }

            return new PlayerNameLayout(
                name.Substring(0, leftLength),
                name.Substring(leftLength, middleLength),
                name.Substring(leftLength + middleLength));
        }
    }
}

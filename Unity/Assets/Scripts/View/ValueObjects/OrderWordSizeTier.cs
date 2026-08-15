// 仕様書: Unity/docs/.sdd/hud/02-order-word-emphasis.md §3.1
// お題の文字サイズを単語長から段階的に決める。
//
// TextMeshPro の Auto Size は使わない：
//  - お題は打鍵1回ごとに SetWord / SetTypedProgress が呼ばれ、そのたび再レイアウトが走る（WebGL で効く）
//  - 段階制なら**同じ長さの単語が常に同じ大きさ**になる。単語が変わるたびに微妙にサイズが動くのは読む側の負担

namespace Takoda99.View.ValueObjects
{
    /// <summary>お題の文字サイズの段階。</summary>
    public enum OrderWordSizeTier
    {
        /// <summary>1〜3文字。最大サイズ。画面の主役として成立させる。</summary>
        Large,

        /// <summary>4〜6文字。</summary>
        Medium,

        /// <summary>7文字以上。可読性を優先して小さくする。</summary>
        Small,
    }

    public static class OrderWordSizeRule
    {
        /// <summary>Large と Medium の境界（この文字数までが Large）。</summary>
        public const int DefaultLargeMaxLength = 3;

        /// <summary>Medium と Small の境界（この文字数までが Medium）。</summary>
        public const int DefaultMediumMaxLength = 6;

        /// <summary>単語の文字数から段階を決める。空文字は Large（枠だけが出る状態）。</summary>
        public static OrderWordSizeTier From(string word)
            => From(word, DefaultLargeMaxLength, DefaultMediumMaxLength);

        /// <summary>
        /// 閾値を指定して段階を決める。閾値は Inspector 公開値にして実機で詰められるようにする。
        /// </summary>
        public static OrderWordSizeTier From(string word, int largeMaxLength, int mediumMaxLength)
        {
            var length = string.IsNullOrEmpty(word) ? 0 : word.Length;

            if (length <= largeMaxLength)
            {
                return OrderWordSizeTier.Large;
            }

            return length <= mediumMaxLength ? OrderWordSizeTier.Medium : OrderWordSizeTier.Small;
        }
    }
}

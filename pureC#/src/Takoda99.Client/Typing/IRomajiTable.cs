using System.Collections.Generic;

namespace Takoda99.Client.Typing;

/// <summary>打鍵単位（かな1つ、または非かな文字1つ）と、その受理ローマ字候補。</summary>
public readonly struct KanaUnit
{
    public KanaUnit(string kana, IReadOnlyList<string> patterns)
    {
        Kana = kana;
        Patterns = patterns;
    }

    /// <summary>元のかな表記（"た" / "しゃ" / "っ" / "ん" / "ー" / "a"）。表示のハイライト幅に使う。</summary>
    public string Kana { get; }

    /// <summary>この位置で受理するローマ字パターン（文脈解決済み）。すべて小文字。</summary>
    public IReadOnlyList<string> Patterns { get; }
}

/// <summary>
/// ローマ字テーブル。正典は Proto 共有データで、実装はその差し替え口。
/// ハードコードしたテーブルを判定ロジック側に埋め込まないための境界（第6章 §2）。
/// </summary>
public interface IRomajiTable
{
    /// <summary>最長一致で探索するかなの最大文字数（拗音を含むので通常 2）。</summary>
    int MaxKanaLength { get; }

    /// <summary>単一かなの受理パターン（文脈解決前の素の値）。未登録なら空。</summary>
    IReadOnlyList<string> GetPatterns(string kana);

    /// <summary>
    /// お題単語を打鍵単位へ分割し、促音・撥音の文脈を解決した候補付きで返す。
    /// </summary>
    /// <remarks>未登録のかなが現れても例外を投げず、その1文字をそのまま1打鍵単位として返す。</remarks>
    IReadOnlyList<KanaUnit> Segment(string word);
}

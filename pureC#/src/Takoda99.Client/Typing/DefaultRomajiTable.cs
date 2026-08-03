using System;
using System.Collections.Generic;
using System.Linq;

namespace Takoda99.Client.Typing;

/// <summary>
/// 【暫定】Proto 未投入の間のローマ字テーブル。正典は Takoda99-Proto の共有データであり、
/// Proto 投入後はこのクラスを差し替えるだけで済むよう <see cref="IRomajiTable"/> の裏に隠してある
/// （02-romaji-table.md §3.5）。
/// </summary>
public sealed class DefaultRomajiTable : IRomajiTable
{
    public int MaxKanaLength => 2;

    private static readonly IReadOnlyDictionary<string, string[]> Table = BuildTable();

    public IReadOnlyList<string> GetPatterns(string kana)
    {
        return Table.TryGetValue(kana, out var patterns) ? patterns : Array.Empty<string>();
    }

    public IReadOnlyList<KanaUnit> Segment(string word)
    {
        var units = new List<KanaUnit>();
        var i = 0;

        while (i < word.Length)
        {
            var (kana, patterns) = MatchLongest(word, i);
            units.Add(new KanaUnit(kana, patterns));
            i += kana.Length;
        }

        return ResolveContext(units);
    }

    private (string kana, string[] patterns) MatchLongest(string word, int index)
    {
        var maxLen = Math.Min(MaxKanaLength, word.Length - index);
        for (var len = maxLen; len >= 1; len--)
        {
            var candidate = word.Substring(index, len);
            if (Table.TryGetValue(candidate, out var patterns))
            {
                return (candidate, patterns);
            }
        }

        // 未登録：1文字をそのまま1打鍵単位として通す（非かな文字・未収録かな）。
        var single = word.Substring(index, 1);
        return (single, new[] { single.ToLowerInvariant() });
    }

    private static IReadOnlyList<KanaUnit> ResolveContext(List<KanaUnit> raw)
    {
        var result = new List<KanaUnit>(raw.Count);

        for (var i = 0; i < raw.Count; i++)
        {
            var unit = raw[i];
            var next = i + 1 < raw.Count ? raw[i + 1] : (KanaUnit?)null;

            if (unit.Kana == "っ")
            {
                result.Add(new KanaUnit(unit.Kana, ResolveSokuon(next)));
                continue;
            }

            if (unit.Kana == "ん")
            {
                result.Add(new KanaUnit(unit.Kana, ResolveHatsuon(next)));
                continue;
            }

            result.Add(unit);
        }

        return result;
    }

    /// <summary>促音「っ」の文脈解決（02-romaji-table.md §3.2）。</summary>
    private static string[] ResolveSokuon(KanaUnit? next)
    {
        var patterns = new List<string> { "xtu", "ltu", "ltsu", "xtsu" };

        if (next is { } n && n.Patterns.Count > 0)
        {
            var firstConsonant = n.Patterns
                .Select(p => p.Length > 0 ? p[0] : '\0')
                .Where(c => c != '\0' && !IsVowel(c) && c != 'n')
                .Distinct();

            foreach (var c in firstConsonant)
            {
                patterns.Insert(0, c.ToString());
            }
        }

        return patterns.ToArray();
    }

    /// <summary>撥音「ん」の文脈解決（02-romaji-table.md §3.3）。</summary>
    private static string[] ResolveHatsuon(KanaUnit? next)
    {
        var always = new[] { "nn", "xn", "n'" };

        if (next is null)
        {
            return always;
        }

        var nextFirstChars = next.Value.Patterns
            .Select(p => p.Length > 0 ? p[0] : '\0')
            .ToArray();

        // 母音・な行・「ん」で始まる候補が含まれるなら n 単独は不可。
        var followedByVowelOrNaOrN = nextFirstChars.Any(c => IsVowel(c) || c == 'n');

        if (followedByVowelOrNaOrN || next.Value.Kana == "ん")
        {
            return always;
        }

        return new[] { "n" }.Concat(always).ToArray();
    }

    private static bool IsVowel(char c) => c is 'a' or 'i' or 'u' or 'e' or 'o';

    private static Dictionary<string, string[]> BuildTable()
    {
        var table = new Dictionary<string, string[]>
        {
            ["あ"] = new[] { "a" },
            ["い"] = new[] { "i" },
            ["う"] = new[] { "u" },
            ["え"] = new[] { "e" },
            ["お"] = new[] { "o" },

            ["か"] = new[] { "ka" },
            ["き"] = new[] { "ki" },
            ["く"] = new[] { "ku" },
            ["け"] = new[] { "ke" },
            ["こ"] = new[] { "ko" },
            ["が"] = new[] { "ga" },
            ["ぎ"] = new[] { "gi" },
            ["ぐ"] = new[] { "gu" },
            ["げ"] = new[] { "ge" },
            ["ご"] = new[] { "go" },

            ["さ"] = new[] { "sa" },
            ["し"] = new[] { "si", "shi", "ci" },
            ["す"] = new[] { "su" },
            ["せ"] = new[] { "se" },
            ["そ"] = new[] { "so" },
            ["ざ"] = new[] { "za" },
            ["じ"] = new[] { "zi", "ji" },
            ["ず"] = new[] { "zu" },
            ["ぜ"] = new[] { "ze" },
            ["ぞ"] = new[] { "zo" },

            ["た"] = new[] { "ta" },
            ["ち"] = new[] { "ti", "chi" },
            ["つ"] = new[] { "tu", "tsu" },
            ["て"] = new[] { "te" },
            ["と"] = new[] { "to" },
            ["だ"] = new[] { "da" },
            ["ぢ"] = new[] { "di", "ji" },
            ["づ"] = new[] { "du", "zu" },
            ["で"] = new[] { "de" },
            ["ど"] = new[] { "do" },

            ["な"] = new[] { "na" },
            ["に"] = new[] { "ni" },
            ["ぬ"] = new[] { "nu" },
            ["ね"] = new[] { "ne" },
            ["の"] = new[] { "no" },

            ["は"] = new[] { "ha" },
            ["ひ"] = new[] { "hi" },
            ["ふ"] = new[] { "hu", "fu" },
            ["へ"] = new[] { "he" },
            ["ほ"] = new[] { "ho" },
            ["ば"] = new[] { "ba" },
            ["び"] = new[] { "bi" },
            ["ぶ"] = new[] { "bu" },
            ["べ"] = new[] { "be" },
            ["ぼ"] = new[] { "bo" },
            ["ぱ"] = new[] { "pa" },
            ["ぴ"] = new[] { "pi" },
            ["ぷ"] = new[] { "pu" },
            ["ぺ"] = new[] { "pe" },
            ["ぽ"] = new[] { "po" },

            ["ま"] = new[] { "ma" },
            ["み"] = new[] { "mi" },
            ["む"] = new[] { "mu" },
            ["め"] = new[] { "me" },
            ["も"] = new[] { "mo" },

            ["や"] = new[] { "ya" },
            ["ゆ"] = new[] { "yu" },
            ["よ"] = new[] { "yo" },

            ["ら"] = new[] { "ra" },
            ["り"] = new[] { "ri" },
            ["る"] = new[] { "ru" },
            ["れ"] = new[] { "re" },
            ["ろ"] = new[] { "ro" },

            ["わ"] = new[] { "wa" },
            ["を"] = new[] { "wo" },

            ["ぁ"] = new[] { "xa", "la" },
            ["ぃ"] = new[] { "xi", "li" },
            ["ぅ"] = new[] { "xu", "lu" },
            ["ぇ"] = new[] { "xe", "le" },
            ["ぉ"] = new[] { "xo", "lo" },

            ["ー"] = new[] { "-" },
        };

        AddYouonRow(table, "き", "kya", "kyu", "kyo");
        AddYouonRow(table, "ぎ", "gya", "gyu", "gyo");
        AddYouonRow(table, "し", "sya", "syu", "syo", "sha", "shu", "sho");
        AddYouonRow(table, "じ", "zya", "zyu", "zyo", "ja", "ju", "jo");
        AddYouonRow(table, "ち", "tya", "tyu", "tyo", "cha", "chu", "cho");
        AddYouonRow(table, "ぢ", "dya", "dyu", "dyo", "ja", "ju", "jo");
        AddYouonRow(table, "に", "nya", "nyu", "nyo");
        AddYouonRow(table, "ひ", "hya", "hyu", "hyo");
        AddYouonRow(table, "び", "bya", "byu", "byo");
        AddYouonRow(table, "ぴ", "pya", "pyu", "pyo");
        AddYouonRow(table, "み", "mya", "myu", "myo");
        AddYouonRow(table, "り", "rya", "ryu", "ryo");

        return table;
    }

    /// <summary>
    /// 拗音1行分（ゃゅょ）を登録する。canonical（子音+ya/yu/yo）に加え、
    /// 「子音+i+xya系/lya系」という2打鍵単位分割経路と、行固有の追加表記を合成する。
    /// </summary>
    private static void AddYouonRow(
        Dictionary<string, string[]> table,
        string iRowKana,
        string canonicalYa, string canonicalYu, string canonicalYo,
        params string[] extra)
    {
        var iPatterns = table[iRowKana];

        string[] BuildFor(string canonical, string smallSuffix)
        {
            var patterns = new List<string> { canonical };
            foreach (var iPattern in iPatterns)
            {
                patterns.Add(iPattern + "l" + smallSuffix);
                patterns.Add(iPattern + "x" + smallSuffix);
            }

            return patterns.Distinct().ToArray();
        }

        table[iRowKana + "ゃ"] = BuildFor(canonicalYa, "ya").Concat(extra.Where(e => e.EndsWith("a"))).Distinct().ToArray();
        table[iRowKana + "ゅ"] = BuildFor(canonicalYu, "yu").Concat(extra.Where(e => e.EndsWith("u"))).Distinct().ToArray();
        table[iRowKana + "ょ"] = BuildFor(canonicalYo, "yo").Concat(extra.Where(e => e.EndsWith("o"))).Distinct().ToArray();
    }
}

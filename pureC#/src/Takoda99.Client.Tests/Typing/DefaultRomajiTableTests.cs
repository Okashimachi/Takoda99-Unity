using System.Linq;
using Takoda99.Client.Typing;
using Xunit;

namespace Takoda99.Client.Tests.Typing;

public class DefaultRomajiTableTests
{
    private readonly DefaultRomajiTable _table = new();

    [Fact]
    public void たこの分割は2単位()
    {
        var units = _table.Segment("たこ");

        Assert.Equal(2, units.Count);
        Assert.Equal("た", units[0].Kana);
        Assert.Equal("こ", units[1].Kana);
    }

    [Fact]
    public void きゃの分割は1単位()
    {
        var units = _table.Segment("きゃ");

        Assert.Single(units);
        Assert.Equal("きゃ", units[0].Kana);
    }

    [Fact]
    public void しのパターンはsi_shi_ciを含む()
    {
        var patterns = _table.GetPatterns("し");

        Assert.Contains("si", patterns);
        Assert.Contains("shi", patterns);
        Assert.Contains("ci", patterns);
    }

    [Fact]
    public void がっこうの促音はkを含む()
    {
        var units = _table.Segment("がっこう");
        var sokuon = units.Single(u => u.Kana == "っ");

        Assert.Contains("k", sokuon.Patterns);
    }

    [Fact]
    public void たっちの促音はtを含む()
    {
        var units = _table.Segment("たっち");
        var sokuon = units.Single(u => u.Kana == "っ");

        Assert.Contains("t", sokuon.Patterns);
    }

    [Fact]
    public void 語末の促音は子音重ねを含まない()
    {
        var units = _table.Segment("あっ");
        var sokuon = units.Single(u => u.Kana == "っ");

        Assert.All(sokuon.Patterns, p => Assert.True(p is "xtu" or "ltu" or "ltsu" or "xtsu"));
    }

    [Fact]
    public void かんいの撥音はnnを含みn単独を含まない()
    {
        var units = _table.Segment("かんい");
        var hatsuon = units.Single(u => u.Kana == "ん");

        Assert.Contains("nn", hatsuon.Patterns);
        Assert.DoesNotContain("n", hatsuon.Patterns);
    }

    [Fact]
    public void かんじの撥音はnを含む()
    {
        var units = _table.Segment("かんじ");
        var hatsuon = units.Single(u => u.Kana == "ん");

        Assert.Contains("n", hatsuon.Patterns);
    }

    [Fact]
    public void 伸ばし棒はハイフン()
    {
        var units = _table.Segment("ー");

        Assert.Equal(new[] { "-" }, units[0].Patterns);
    }

    [Fact]
    public void 英数字混じりは1文字1単位()
    {
        var units = _table.Segment("Ab1");

        Assert.Equal(3, units.Count);
        Assert.Equal(new[] { "a" }, units[0].Patterns);
        Assert.Equal(new[] { "b" }, units[1].Patterns);
        Assert.Equal(new[] { "1" }, units[2].Patterns);
    }

    [Fact]
    public void 未登録のかなは例外にならず1単位で返る()
    {
        var units = _table.Segment("№");

        Assert.Single(units);
        Assert.Equal("№", units[0].Kana);
    }

    [Fact]
    public void 全パターンが小文字()
    {
        foreach (var word in new[] { "たこやきをつくる", "しゃしんをとる" })
        {
            foreach (var unit in _table.Segment(word))
            {
                foreach (var pattern in unit.Patterns)
                {
                    Assert.Equal(pattern.ToLowerInvariant(), pattern);
                }
            }
        }
    }

    [Theory]
    [InlineData("し", "si")]
    [InlineData("ち", "ti")]
    [InlineData("つ", "tu")]
    [InlineData("ふ", "hu")]
    [InlineData("じ", "zi")]
    public void 正準表記が候補に含まれる(string kana, string canonical)
    {
        Assert.Contains(canonical, _table.GetPatterns(kana));
    }

    [Fact]
    public void しゃの正準表記はsya()
    {
        var units = _table.Segment("しゃ");
        Assert.Contains("sya", units[0].Patterns);
    }
}

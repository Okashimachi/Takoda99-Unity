using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests;

public class PlayerNameLayoutTests
{
    [Fact]
    public void 六文字はそのまま二文字ずつに割れる()
    {
        var layout = PlayerNameLayout.From("たこやきや");  // 5文字 → +屋 → 6文字
        Assert.Equal("たこ", layout.Left);
        Assert.Equal("やき", layout.Middle);
        Assert.Equal("や屋", layout.Right);
    }

    [Fact]
    public void 三の倍数は屋を足さない()
    {
        var layout = PlayerNameLayout.From("たこやきだいすき"); // 8文字 → +屋 → 9文字
        Assert.Equal("たこや", layout.Left);
        Assert.Equal("きだい", layout.Middle);
        Assert.Equal("すき屋", layout.Right);

        var exact = PlayerNameLayout.From("たこやき屋六"); // 6文字ちょうど
        Assert.Equal("たこ", exact.Left);
        Assert.Equal("やき", exact.Middle);
        Assert.Equal("屋六", exact.Right);
    }

    [Fact]
    public void 二文字は屋を足して一文字ずつになる()
    {
        var layout = PlayerNameLayout.From("たこ");
        Assert.Equal("た", layout.Left);
        Assert.Equal("こ", layout.Middle);
        Assert.Equal("屋", layout.Right);
    }

    [Fact]
    public void 四文字は屋を足して右に屋だけを残す()
    {
        var layout = PlayerNameLayout.From("たこ焼き"); // +屋 → 5文字 → 2/2/1
        Assert.Equal("たこ", layout.Left);
        Assert.Equal("焼き", layout.Middle);
        Assert.Equal("屋", layout.Right);
    }

    [Fact]
    public void 一文字は屋を足さず中央だけに出す()
    {
        var layout = PlayerNameLayout.From("た");
        Assert.Equal(string.Empty, layout.Left);
        Assert.Equal("た", layout.Middle);
        Assert.Equal(string.Empty, layout.Right);
    }

    [Fact]
    public void 空とnullは全枠が空になる()
    {
        foreach (var name in new[] { string.Empty, null })
        {
            var layout = PlayerNameLayout.From(name);
            Assert.Equal(string.Empty, layout.Left);
            Assert.Equal(string.Empty, layout.Middle);
            Assert.Equal(string.Empty, layout.Right);
        }
    }

    [Fact]
    public void 分割結果を連結すると元の名前と接尾辞になる()
    {
        foreach (var name in new[] { "あ", "あい", "あいう", "あいうえ", "あいうえお", "あいうえおか" })
        {
            var layout = PlayerNameLayout.From(name);
            var joined = layout.Left + layout.Middle + layout.Right;
            Assert.StartsWith(name, joined);
            Assert.True(joined.Length - name.Length <= 1);
        }
    }
}

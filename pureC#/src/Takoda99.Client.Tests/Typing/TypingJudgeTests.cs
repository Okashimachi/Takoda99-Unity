using System.Collections.Generic;
using Takoda99.Client.Typing;
using Xunit;

namespace Takoda99.Client.Tests.Typing;

public class TypingJudgeTests
{
    private readonly FakeClock _clock = new();
    private readonly TypingJudge _judge;

    public TypingJudgeTests()
    {
        _judge = new TypingJudge(new DefaultRomajiTable(), _clock);
    }

    private static IReadOnlyList<string> Words(params string[] words) => words;

    private IReadOnlyList<KeyResult> Type(string input)
    {
        var results = new List<KeyResult>();
        foreach (var c in input)
        {
            results.Add(_judge.PressKey(c));
        }

        return results;
    }

    [Fact]
    public void 単純_たこtako_WordClearedで終わる()
    {
        _judge.BeginOrder("c1", Words("たこ"));

        var results = Type("tako");

        Assert.Equal(KeyResult.Correct, results[0]);
        Assert.Equal(KeyResult.Correct, results[1]);
        Assert.Equal(KeyResult.Correct, results[2]);
        Assert.Equal(KeyResult.OrderCleared, results[3]);
    }

    [Theory]
    [InlineData("shi")]
    [InlineData("si")]
    public void 複数表記_しはどちらも成立する(string input)
    {
        _judge.BeginOrder("c1", Words("し"));
        var results = Type(input);
        Assert.Equal(KeyResult.OrderCleared, results[^1]);
    }

    [Theory]
    [InlineData("takko")]
    [InlineData("taxtuko")]
    public void 促音_たっこはどちらも成立する(string input)
    {
        _judge.BeginOrder("c1", Words("たっこ"));
        var results = Type(input);
        Assert.Equal(KeyResult.OrderCleared, results[^1]);
    }

    [Fact]
    public void 撥音_母音前_かんいはkanniのみ成立する()
    {
        _judge.BeginOrder("c1", Words("かんい"));
        var results = Type("kanni");
        Assert.Equal(KeyResult.OrderCleared, results[^1]);
    }

    [Fact]
    public void 撥音_母音前_かんいのkaniは不成立でミスになる()
    {
        _judge.BeginOrder("c1", Words("かんい"));
        var results = Type("kani");
        Assert.Contains(KeyResult.Miss, results);
    }

    [Fact]
    public void 撥音_子音前_かんじはkanziで成立する()
    {
        _judge.BeginOrder("c1", Words("かんじ"));
        var results = Type("kanzi");
        Assert.Equal(KeyResult.OrderCleared, results[^1]);
    }

    [Fact]
    public void ミス後の復帰_missCountが1になる()
    {
        _judge.BeginOrder("c1", Words("たこ"));
        Type("tapko");

        var report = _judge.BuildReport();
        Assert.NotNull(report);
        Assert.Equal(1, report!.Value.MissCount);
    }

    [Fact]
    public void 大文字_TAKOでも成立する()
    {
        _judge.BeginOrder("c1", Words("たこ"));
        var results = Type("TAKO");
        Assert.Equal(KeyResult.OrderCleared, results[^1]);
    }

    [Fact]
    public void 対象外キーはmissCountを増やさない()
    {
        _judge.BeginOrder("c1", Words("たこ"));
        Type("ta");
        _judge.PressKey('\n');
        Type("ko");

        var report = _judge.BuildReport();
        Assert.Equal(0, report!.Value.MissCount);
    }

    [Fact]
    public void 注文横断_missCountはリセットされない()
    {
        _judge.BeginOrder("c1", Words("たこ", "いか"));
        Type("tapko"); // "たこ" に1ミス
        Type("ixka");  // "いか" に1ミス（存在しないprefixで即ミス）

        var report = _judge.BuildReport();
        Assert.Equal(2, report!.Value.MissCount);
    }

    [Fact]
    public void 拗音の分割入力_きゃはどちらも成立する()
    {
        _judge.BeginOrder("c1", Words("きゃ"));
        Assert.Equal(KeyResult.OrderCleared, Type("kya")[^1]);

        _judge.BeginOrder("c2", Words("きゃ"));
        Assert.Equal(KeyResult.OrderCleared, Type("kilya")[^1]);
    }

    [Fact]
    public void elapsedMsはBeginOrderからの経過を含む()
    {
        _judge.BeginOrder("c1", Words("たこ"));
        _clock.Advance(500);
        Type("tako");

        var report = _judge.BuildReport();
        Assert.True(report!.Value.ElapsedMs >= 500);
    }

    [Fact]
    public void 中断後のBuildReportはnull()
    {
        _judge.BeginOrder("c1", Words("たこ"));
        Type("ta");
        _judge.AbortOrder();

        Assert.Null(_judge.BuildReport());
        Assert.True(_judge.IsIdle);
    }

    [Fact]
    public void Idle中の打鍵はIgnoredでmissCountも増えない()
    {
        var result = _judge.PressKey('t');
        Assert.Equal(KeyResult.Ignored, result);
        Assert.True(_judge.IsIdle);
    }

    [Fact]
    public void wordsが空ならIdleのまま()
    {
        _judge.BeginOrder("c1", Words());
        Assert.True(_judge.IsIdle);
    }

    [Fact]
    public void OrderCleared後のPressKeyはIgnored()
    {
        _judge.BeginOrder("c1", Words("たこ"));
        Type("tako");

        Assert.Equal(KeyResult.Ignored, _judge.PressKey('t'));
    }
}

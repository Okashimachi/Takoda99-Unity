using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests;

public class MatchmakingCountdownStateTests
{
    [Fact]
    public void カウントダウン中は秒へ切り上げて表示する()
    {
        var state = MatchmakingCountdownState.From(countdownMs: 15000, remainingToDeadlineMs: 15000);

        Assert.True(state.IsCountingDown);
        Assert.False(state.IsComplete);
        Assert.Equal(15, state.RemainingSeconds);

        // 端数は切り上げる（14.2秒なら「のこり15秒」）。
        Assert.Equal(15, MatchmakingCountdownState.From(14200, 14200).RemainingSeconds);
        Assert.Equal(1, MatchmakingCountdownState.From(1, 1).RemainingSeconds);
    }

    [Fact]
    public void countdownMs欠落は0秒と表示しない()
    {
        // 待機中は countdownMs がキーごと来ない。0 と表示すると始まらない画面になる（§3）。
        var state = MatchmakingCountdownState.From(countdownMs: null, remainingToDeadlineMs: null);

        Assert.False(state.IsCountingDown);
        Assert.False(state.IsComplete);
    }

    [Fact]
    public void 締切を過ぎたら完了になる()
    {
        // サーバーは尽きた瞬間に 0 を送り直さないので countdownMs には最後の値が残る。
        // それでも締切を過ぎていれば完了として扱う。
        var state = MatchmakingCountdownState.From(countdownMs: 1000, remainingToDeadlineMs: -20);

        Assert.False(state.IsCountingDown);
        Assert.True(state.IsComplete);
        Assert.Equal(0, state.RemainingSeconds);
    }

    [Fact]
    public void 締切ちょうども完了として扱う()
    {
        Assert.True(MatchmakingCountdownState.From(1000, 0).IsComplete);
    }

    [Fact]
    public void 時間が残っているうちの欠落は中断であって完了ではない()
    {
        // minPlayers を割り込んでカウントダウンが中断された場合（§5.2）。
        var state = MatchmakingCountdownState.From(countdownMs: null, remainingToDeadlineMs: 8000);

        Assert.False(state.IsComplete);
        Assert.False(state.IsCountingDown);
    }

    [Fact]
    public void 負の残りは0秒に丸める()
    {
        Assert.Equal(0, MatchmakingCountdownState.From(-500, 5000).RemainingSeconds);
    }
}

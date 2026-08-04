using System;
using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.Client.Testing;
using Xunit;

namespace Takoda99.Client.Tests.Testing;

public class ScenarioPlayerTests
{
    private static Scenario BuildScenario() => Scenario.Parse("""
    {
      "name": "sample",
      "steps": [
        { "atMs": 0,    "kind": "connection", "state": "Connected" },
        { "atMs": 0,    "kind": "receive", "type": "MatchStart", "payload": {} },
        { "atMs": 1000, "kind": "receive", "type": "CustomerArrived", "payload": { "customerId": "c-01" } },
        { "atMs": 1500, "kind": "input",   "keys": "ab" },
        { "atMs": 9000, "kind": "receive", "type": "EvaluationUpdate", "payload": { "normalized": 0.21 } },
        { "atMs": 9500, "kind": "wait" }
      ]
    }
    """);

    [Fact]
    public void RunToEndで全ステップが実行される()
    {
        var player = new ScenarioPlayer(BuildScenario());

        player.RunToEnd();

        Assert.Equal(6, player.Executed.Count);
        Assert.False(player.HasPendingSteps);
        Assert.Equal(9500, player.CurrentMs);
    }

    [Fact]
    public void AdvanceToはAtMs以下の未実行ステップのみ実行する()
    {
        var player = new ScenarioPlayer(BuildScenario());

        player.AdvanceTo(1000);

        Assert.Equal(3, player.Executed.Count);
        Assert.Equal(1000, player.CurrentMs);
        Assert.True(player.HasPendingSteps);
    }

    [Fact]
    public void receiveステップはOnReceiveRawへEnvelope形式の生JSONを流す()
    {
        var player = new ScenarioPlayer(BuildScenario());
        var received = new List<string>();
        player.OnReceiveRaw += received.Add;

        player.AdvanceTo(0);

        Assert.Single(received);
        Assert.Contains("\"type\":\"MatchStart\"", received[0]);
        Assert.Contains("\"payload\":{}", received[0]);
    }

    [Fact]
    public void inputステップは1文字ずつOnCharKeyを発火する()
    {
        var player = new ScenarioPlayer(BuildScenario());
        var keys = new List<char>();
        player.OnCharKey += keys.Add;

        player.AdvanceTo(1500);

        Assert.Equal(new[] { 'a', 'b' }, keys);
    }

    [Fact]
    public void connectionステップはStateとOnConnectionChangedを更新する()
    {
        var player = new ScenarioPlayer(BuildScenario());
        var changes = new List<(ConnectionState State, string? Error)>();
        player.OnConnectionChanged += (s, e) => changes.Add((s, e));

        player.AdvanceTo(0);

        Assert.Equal(ConnectionState.Connected, player.State);
        Assert.Single(changes);
        Assert.Equal(ConnectionState.Connected, changes[0].State);
    }

    [Fact]
    public void ステップ実行時点のCurrentMsはAtMsと一致する()
    {
        var player = new ScenarioPlayer(BuildScenario());
        var observedAtCustomerArrived = -1L;
        player.OnReceiveRaw += json =>
        {
            if (json.Contains("CustomerArrived"))
            {
                observedAtCustomerArrived = player.CurrentMs;
            }
        };

        player.RunToEnd();

        Assert.Equal(1000, observedAtCustomerArrived);
    }

    [Fact]
    public void AdvanceToに過去の時刻を渡すとArgumentOutOfRangeException()
    {
        var player = new ScenarioPlayer(BuildScenario());
        player.AdvanceTo(1000);

        Assert.Throws<ArgumentOutOfRangeException>(() => player.AdvanceTo(500));
    }

    [Fact]
    public void 空のシナリオでRunToEndは何もしない()
    {
        var player = new ScenarioPlayer(Scenario.Parse("""{ "name": "empty", "steps": [] }"""));

        player.RunToEnd();

        Assert.Equal(0, player.CurrentMs);
        Assert.False(player.HasPendingSteps);
    }

    [Fact]
    public void MonotonicMsとWallClockUnixMsはCurrentMsに追従する()
    {
        var player = new ScenarioPlayer(BuildScenario());
        var wallClockAtZero = player.WallClockUnixMs;

        player.AdvanceTo(1000);

        Assert.Equal(1000, player.MonotonicMs);
        Assert.Equal(wallClockAtZero + 1000, player.WallClockUnixMs);
    }

    [Fact]
    public void WallClockUnixMsは実時間に依存せず毎回同じ値になる()
    {
        var player1 = new ScenarioPlayer(BuildScenario());
        var player2 = new ScenarioPlayer(BuildScenario());

        player1.AdvanceTo(1000);
        player2.AdvanceTo(1000);

        Assert.Equal(player1.WallClockUnixMs, player2.WallClockUnixMs);
    }

    [Fact]
    public void 最後のステップより先へ進めた後でもRunToEndは例外を投げない()
    {
        var player = new ScenarioPlayer(BuildScenario());
        player.AdvanceTo(20000);

        player.RunToEnd();

        Assert.False(player.HasPendingSteps);
        Assert.Equal(20000, player.CurrentMs);
        Assert.Equal(6, player.Executed.Count);
    }

    [Fact]
    public void Sendは記録されるだけでOnReceiveRawへは流れない()
    {
        var player = new ScenarioPlayer(BuildScenario());

        player.Send("OrderServed", new { customerId = "c-01" });

        Assert.Single(player.Sent);
        Assert.Equal("OrderServed", player.Sent[0].Type);
    }

    [Fact]
    public void 同一シナリオを2回実行してもSentとExecutedが完全一致する()
    {
        var player1 = new ScenarioPlayer(BuildScenario());
        player1.RunToEnd();
        player1.Send("OrderServed", new { customerId = "c-01" });

        var player2 = new ScenarioPlayer(BuildScenario());
        player2.RunToEnd();
        player2.Send("OrderServed", new { customerId = "c-01" });

        Assert.Equal(player1.Executed.Count, player2.Executed.Count);
        for (var i = 0; i < player1.Executed.Count; i++)
        {
            Assert.Equal(player1.Executed[i].AtMs, player2.Executed[i].AtMs);
            Assert.Equal(player1.Executed[i].Kind, player2.Executed[i].Kind);
        }

        Assert.Equal(player1.Sent, player2.Sent);
    }

    [Fact]
    public void 未知typeも壊れたpayloadもそのまま流れる()
    {
        var scenario = Scenario.Parse("""
        {
          "name": "resilience",
          "steps": [
            { "atMs": 0, "kind": "receive", "type": "SomeFutureMessage", "payload": { "x": 1 } }
          ]
        }
        """);
        var player = new ScenarioPlayer(scenario);
        var received = new List<string>();
        player.OnReceiveRaw += received.Add;

        player.RunToEnd();

        Assert.Single(received);
        Assert.Contains("SomeFutureMessage", received[0]);
    }
}

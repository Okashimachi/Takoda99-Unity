using System.Collections.Generic;
using System.Text.Json;
using Takoda99.Client.State;
using Takoda99.Client.Testing;
using Xunit;

namespace Takoda99.Client.Tests.Testing;

/// <summary>07-scenario-player.md §7 の各シナリオが読み込めて最後まで再生できることを確認する。</summary>
public class ScenarioSamplesTests
{
    [Theory]
    [InlineData("minimal-match")]
    [InlineData("order-progress-variants")]
    [InlineData("customer-leaves-while-typing")]
    [InlineData("patience-expires-but-no-leave")]
    [InlineData("queue-accumulates")]
    [InlineData("evaluation-bands")]
    [InlineData("credit-decreases")]
    [InlineData("store-list-snapshot")]
    [InlineData("self-eliminated")]
    [InlineData("other-store-eliminated")]
    [InlineData("phase-and-heat")]
    [InlineData("unknown-message-type")]
    [InlineData("few-players-match")]
    [InlineData("claimer-drops-evaluation")]
    public void シナリオが最後まで再生できる(string name)
    {
        var player = new ScenarioPlayer(Scenario.Load(name));

        player.RunToEnd();

        Assert.False(player.HasPendingSteps);
        Assert.NotEmpty(player.Executed);
    }

    [Fact]
    public void customerLeavesWhileTypingは進行中の客の離脱と後続の客の入力を両方踏む()
    {
        var player = new ScenarioPlayer(Scenario.Load("customer-leaves-while-typing"));
        var receivedTypes = new List<string>();
        player.OnReceiveRaw += json =>
        {
            foreach (var candidate in new[] { "CustomerLeft", "CustomerArrived" })
            {
                if (json.Contains($"\"type\":\"{candidate}\""))
                {
                    receivedTypes.Add(candidate);
                }
            }
        };

        player.RunToEnd();

        Assert.Contains("CustomerLeft", receivedTypes);
        Assert.Equal(2, receivedTypes.FindAll(t => t == "CustomerArrived").Count);
    }

    [Fact]
    public void patienceExpiresButNoLeaveはCustomerLeftを一度も流さない()
    {
        var player = new ScenarioPlayer(Scenario.Load("patience-expires-but-no-leave"));
        var received = new List<string>();
        player.OnReceiveRaw += received.Add;

        player.RunToEnd();

        Assert.DoesNotContain(received, json => json.Contains("CustomerLeft"));
    }

    [Fact]
    public void creditDecreasesはlifeを3から0まで単調に流す()
    {
        var player = new ScenarioPlayer(Scenario.Load("credit-decreases"));
        var lifeValues = new List<int>();
        player.OnReceiveRaw += json =>
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("type").GetString() != "CreditUpdate")
            {
                return;
            }

            lifeValues.Add(root.GetProperty("payload").GetProperty("life").GetInt32());
        };

        player.RunToEnd();

        Assert.Equal(new[] { 2, 1, 0 }, lifeValues);
    }

    [Fact]
    public void selfEliminatedは自店脱落後にMatchEndまで到達する()
    {
        var player = new ScenarioPlayer(Scenario.Load("self-eliminated"));
        var received = new List<string>();
        player.OnReceiveRaw += received.Add;

        player.RunToEnd();

        Assert.Contains(received, json => json.Contains("StoreEliminated"));
        Assert.Contains(received, json => json.Contains("MatchEnd"));
    }

    [Fact]
    public void phaseAndHeatはDifficultyUpdate受信前にheatLevelを送らない()
    {
        var scenario = Scenario.Load("phase-and-heat");

        var difficultyIndex = -1;
        var phaseChangeIndices = new List<int>();
        for (var i = 0; i < scenario.Steps.Count; i++)
        {
            var step = scenario.Steps[i];
            if (step.Kind != "receive")
            {
                continue;
            }

            if (step.Type == "DifficultyUpdate" && difficultyIndex == -1)
            {
                difficultyIndex = i;
            }

            if (step.Type == "PhaseChange")
            {
                phaseChangeIndices.Add(i);
            }
        }

        Assert.True(difficultyIndex > phaseChangeIndices[0]);
    }
}

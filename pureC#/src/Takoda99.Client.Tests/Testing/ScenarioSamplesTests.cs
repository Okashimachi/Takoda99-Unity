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
    [InlineData("elimination-batch-while-typing")]
    [InlineData("queue-accumulates")]
    [InlineData("score-progresses")]
    [InlineData("ranking-snapshot-and-delta")]
    [InlineData("cull-countdown")]
    [InlineData("self-eliminated")]
    [InlineData("other-store-eliminated")]
    [InlineData("phase-and-heat")]
    [InlineData("unknown-message-type")]
    [InlineData("few-players-match")]
    public void シナリオが最後まで再生できる(string name)
    {
        var player = new ScenarioPlayer(Scenario.Load(name));

        player.RunToEnd();

        Assert.False(player.HasPendingSteps);
        Assert.NotEmpty(player.Executed);
    }

    /// <summary>
    /// 本選に残る唯一の中断経路。打鍵中に自店を含むバッチが届き、後続の客の入力へ移れること。
    /// 予選の customer-leaves-while-typing を置き換える（客は逃げなくなった）。
    /// </summary>
    [Fact]
    public void eliminationBatchWhileTypingは自店を含むバッチと後続の入力を両方踏む()
    {
        var player = new ScenarioPlayer(Scenario.Load("elimination-batch-while-typing"));
        var receivedTypes = new List<string>();
        player.OnReceiveRaw += json =>
        {
            foreach (var candidate in new[] { "StoreEliminatedBatch", "CustomerArrived" })
            {
                if (json.Contains($"\"type\":\"{candidate}\""))
                {
                    receivedTypes.Add(candidate);
                }
            }
        };

        player.RunToEnd();

        Assert.Contains("StoreEliminatedBatch", receivedTypes);
        Assert.Equal(2, receivedTypes.FindAll(t => t == "CustomerArrived").Count);
    }

    /// <summary>本選では客が逃げないので、廃止済みメッセージがサンプルに残っていないこと。</summary>
    [Theory]
    [InlineData("CustomerLeft")]
    [InlineData("CreditUpdate")]
    [InlineData("StoreListUpdate")]
    public void 廃止済みメッセージはどのシナリオにも含まれない(string obsoleteType)
    {
        foreach (var name in new[]
                 {
                     "minimal-match", "order-progress-variants", "elimination-batch-while-typing",
                     "queue-accumulates", "score-progresses", "ranking-snapshot-and-delta",
                     "cull-countdown", "self-eliminated", "other-store-eliminated",
                     "phase-and-heat", "unknown-message-type", "few-players-match",
                 })
        {
            var scenario = Scenario.Load(name);
            Assert.DoesNotContain(scenario.Steps, s => s.Type == obsoleteType);
        }
    }

    [Fact]
    public void scoreProgressesは負値から始まりscoreを単調に流す()
    {
        var player = new ScenarioPlayer(Scenario.Load("score-progresses"));
        var scores = new List<int>();
        player.OnReceiveRaw += json =>
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("type").GetString() != "EvaluationUpdate")
            {
                return;
            }

            scores.Add(root.GetProperty("payload").GetProperty("score").GetInt32());
        };

        player.RunToEnd();

        Assert.Equal(new[] { -40, 160, 780, 1240 }, scores);
    }

    /// <summary>
    /// 120秒の配信順序（StoreEliminatedBatch → PersonalResult → … → MatchEnd）を踏むこと。
    /// 個人成績は脱落した瞬間に届き、MatchEnd を待たない。
    /// </summary>
    [Fact]
    public void selfEliminatedは脱落直後にPersonalResultを受けMatchEndまで到達する()
    {
        var scenario = Scenario.Load("self-eliminated");
        var order = new List<string>();
        foreach (var step in scenario.Steps)
        {
            if (step.Kind == "receive" && step.Type is "StoreEliminatedBatch" or "PersonalResult" or "MatchEnd")
            {
                order.Add(step.Type!);
            }
        }

        Assert.Equal(new[] { "StoreEliminatedBatch", "PersonalResult", "MatchEnd" }, order);

        var player = new ScenarioPlayer(scenario);
        player.RunToEnd();
        Assert.False(player.HasPendingSteps);
    }

    /// <summary>差分は rank を運ばないので、表示順はクライアントが決める。</summary>
    [Fact]
    public void rankingDeltaのペイロードにrankが含まれない()
    {
        var scenario = Scenario.Load("ranking-snapshot-and-delta");

        foreach (var step in scenario.Steps)
        {
            if (step.Kind != "receive" || step.Type != "RankingDelta" || step.Payload is not { } payload)
            {
                continue;
            }

            using var document = JsonDocument.Parse(payload.GetRawText());
            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                Assert.False(entry.TryGetProperty("rank", out _));
            }
        }
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

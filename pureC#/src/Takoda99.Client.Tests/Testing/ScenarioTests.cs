using Takoda99.Client.State;
using Takoda99.Client.Testing;
using Xunit;

namespace Takoda99.Client.Tests.Testing;

public class ScenarioTests
{
    [Fact]
    public void 正常なJSONをパースできる()
    {
        var scenario = Scenario.Parse("""
        {
          "name": "sample",
          "description": "desc",
          "steps": [
            { "atMs": 0, "kind": "connection", "state": "Connected" },
            { "atMs": 100, "kind": "receive", "type": "MatchStart", "payload": {} },
            { "atMs": 200, "kind": "input", "keys": "ab" },
            { "atMs": 200, "kind": "wait" }
          ]
        }
        """);

        Assert.Equal("sample", scenario.Name);
        Assert.Equal("desc", scenario.Description);
        Assert.Equal(4, scenario.Steps.Count);
        Assert.Equal(ConnectionState.Connected, scenario.Steps[0].State);
        Assert.Equal("MatchStart", scenario.Steps[1].Type);
        Assert.Equal("ab", scenario.Steps[2].Keys);
    }

    [Fact]
    public void atMsが昇順でないとScenarioFormatException()
    {
        var json = """
        {
          "name": "bad",
          "steps": [
            { "atMs": 100, "kind": "wait" },
            { "atMs": 50, "kind": "wait" }
          ]
        }
        """;

        Assert.Throws<ScenarioFormatException>(() => Scenario.Parse(json));
    }

    [Fact]
    public void 未知のkindでScenarioFormatException()
    {
        var json = """
        {
          "name": "bad",
          "steps": [
            { "atMs": 0, "kind": "teleport" }
          ]
        }
        """;

        Assert.Throws<ScenarioFormatException>(() => Scenario.Parse(json));
    }

    [Fact]
    public void 未知のtypeはそのまま読み込める()
    {
        var json = """
        {
          "name": "forward-compat",
          "steps": [
            { "atMs": 0, "kind": "receive", "type": "SomeFutureMessage", "payload": { "x": 1 } }
          ]
        }
        """;

        var scenario = Scenario.Parse(json);

        Assert.Equal("SomeFutureMessage", scenario.Steps[0].Type);
    }

    [Fact]
    public void 空のstepsは正常()
    {
        var scenario = Scenario.Parse("""{ "name": "empty", "steps": [] }""");

        Assert.Empty(scenario.Steps);
    }

    [Fact]
    public void testdataからシナリオファイルを読み込める()
    {
        var scenario = Scenario.Load("minimal-match");

        Assert.Equal("minimal-match", scenario.Name);
        Assert.NotEmpty(scenario.Steps);
    }
}

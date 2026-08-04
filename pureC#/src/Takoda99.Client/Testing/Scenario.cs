using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Takoda99.Client.State;

namespace Takoda99.Client.Testing;

/// <summary>シナリオ内の1ステップ（07-scenario-player.md §4）。</summary>
public sealed class ScenarioStep
{
    public long AtMs { get; init; }

    /// <summary>receive / input / connection / wait。</summary>
    public string Kind { get; init; } = "";

    public string? Type { get; init; }

    public JsonElement? Payload { get; init; }

    public string? Keys { get; init; }

    public ConnectionState? State { get; init; }

    public string? Error { get; init; }
}

/// <summary>サンプルデータ（シナリオ）1本分（07-scenario-player.md §3-4）。</summary>
public sealed class Scenario
{
    private static readonly HashSet<string> KnownKinds = new(StringComparer.Ordinal)
    {
        "receive", "input", "connection", "wait",
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Name { get; init; } = "";

    public string Description { get; init; } = "";

    public IReadOnlyList<ScenarioStep> Steps { get; init; } = Array.Empty<ScenarioStep>();

    /// <summary>JSON文字列から読み込む。形式不正なら <see cref="ScenarioFormatException"/>。</summary>
    public static Scenario Parse(string json)
    {
        RawScenario? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawScenario>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ScenarioFormatException($"シナリオJSONの解析に失敗しました: {ex.Message}");
        }

        if (raw is null)
        {
            throw new ScenarioFormatException("シナリオJSONが空です。");
        }

        var steps = new List<ScenarioStep>();
        var lastAtMs = long.MinValue;

        foreach (var rawStep in raw.Steps ?? new List<RawStep>())
        {
            if (rawStep.AtMs < lastAtMs)
            {
                throw new ScenarioFormatException(
                    $"steps は atMs 昇順である必要があります（{rawStep.AtMs} < {lastAtMs}）。");
            }

            lastAtMs = rawStep.AtMs;

            if (rawStep.Kind is null || !KnownKinds.Contains(rawStep.Kind))
            {
                throw new ScenarioFormatException($"未知の kind です: '{rawStep.Kind}'");
            }

            ConnectionState? state = null;
            if (rawStep.Kind == "connection")
            {
                if (rawStep.State is null || !Enum.TryParse(rawStep.State, out ConnectionState parsed))
                {
                    throw new ScenarioFormatException($"connection ステップの state が不正です: '{rawStep.State}'");
                }

                state = parsed;
            }

            steps.Add(new ScenarioStep
            {
                AtMs = rawStep.AtMs,
                Kind = rawStep.Kind,
                Type = rawStep.Type,
                Payload = rawStep.Payload,
                Keys = rawStep.Keys,
                State = state,
                Error = rawStep.Error,
            });
        }

        return new Scenario
        {
            Name = raw.Name ?? "",
            Description = raw.Description ?? "",
            Steps = steps,
        };
    }

    /// <summary>testdata/scenarios/{name}.json を読み込む。</summary>
    public static Scenario Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "testdata", "scenarios", $"{name}.json");
        if (!File.Exists(path))
        {
            throw new ScenarioFormatException($"シナリオファイルが見つかりません: {path}");
        }

        return Parse(File.ReadAllText(path));
    }

    private sealed class RawScenario
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<RawStep>? Steps { get; set; }
    }

    private sealed class RawStep
    {
        public long AtMs { get; set; }
        public string? Kind { get; set; }
        public string? Type { get; set; }
        public JsonElement? Payload { get; set; }
        public string? Keys { get; set; }
        public string? State { get; set; }
        public string? Error { get; set; }
    }
}

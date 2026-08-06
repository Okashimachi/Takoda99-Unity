using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Takoda99.Client.Lifecycle;
using Takoda99.Client.Net;
using Takoda99.Client.State;

namespace Takoda99.Client.Testing;

/// <summary>
/// シナリオを <see cref="INetworkClient"/> / <see cref="IInputSource"/> / <see cref="IClock"/> として
/// 供給する再生機（07-scenario-player.md）。サーバーへ接続せず、決定論的にシナリオを進める。
/// </summary>
public sealed class ScenarioPlayer : INetworkClient, IInputSource, IClock, IDisposable
{
    // WallClockUnixMs の固定基準時刻。clientTimestamp が毎回変わってテストが不安定になるのを防ぐ（§5）。
    private const long WallClockBaseUnixMs = 1_700_000_000_000;

    private readonly Scenario _scenario;
    private readonly List<ScenarioStep> _executed = new();
    private readonly List<(string Type, object Payload)> _sent = new();
    private int _nextIndex;

    public ScenarioPlayer(Scenario scenario)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
    }

    /// <summary>現在のシナリオ内時刻（ms）。</summary>
    public long CurrentMs { get; private set; }

    /// <summary>まだ実行していないステップが残っているか。</summary>
    public bool HasPendingSteps => _nextIndex < _scenario.Steps.Count;

    /// <summary>実行済みステップの記録（テストの事後検証・デバッグ用）。</summary>
    public IReadOnlyList<ScenarioStep> Executed => _executed;

    /// <summary>クライアントが送信した C2S メッセージ（OrderServed 等）の記録。</summary>
    public IReadOnlyList<(string Type, object Payload)> Sent => _sent;

    /// <summary>指定時刻まで進め、その時刻以下の未実行ステップをすべて実行する。</summary>
    public void AdvanceTo(long atMs)
    {
        if (atMs < CurrentMs)
        {
            throw new ArgumentOutOfRangeException(nameof(atMs), atMs, "過去の時刻へは巻き戻せません。");
        }

        while (_nextIndex < _scenario.Steps.Count && _scenario.Steps[_nextIndex].AtMs <= atMs)
        {
            var step = _scenario.Steps[_nextIndex];
            CurrentMs = step.AtMs;
            Execute(step);
            _executed.Add(step);
            _nextIndex++;
        }

        CurrentMs = atMs;
    }

    /// <summary>相対時間ぶん進める。</summary>
    public void Advance(long deltaMs) => AdvanceTo(CurrentMs + deltaMs);

    /// <summary>最後のステップまで一気に実行する。</summary>
    public void RunToEnd()
    {
        if (_scenario.Steps.Count == 0)
        {
            return;
        }

        // 既に最後のステップより先へ進めていても巻き戻さない（AdvanceTo は過去の時刻を拒否する）。
        var lastAtMs = _scenario.Steps[_scenario.Steps.Count - 1].AtMs;
        AdvanceTo(lastAtMs > CurrentMs ? lastAtMs : CurrentMs);
    }

    private void Execute(ScenarioStep step)
    {
        switch (step.Kind)
        {
            case "receive":
                OnReceiveRaw?.Invoke(BuildRawJson(step));
                break;
            case "input":
                foreach (var ch in step.Keys ?? "")
                {
                    OnCharKey?.Invoke(ch);
                }
                break;
            case "connection":
                if (step.State is { } state)
                {
                    State = state;
                }

                OnConnectionChanged?.Invoke(State, step.Error);
                break;
            case "wait":
                break;
        }
    }

    private static string BuildRawJson(ScenarioStep step)
    {
        var envelope = new JsonObject
        {
            ["type"] = step.Type,
            ["payload"] = step.Payload is { } payload
                ? JsonNode.Parse(payload.GetRawText())
                : null,
        };
        return envelope.ToJsonString();
    }

    public void Dispose()
    {
        OnReceiveRaw = null;
        OnConnectionChanged = null;
        OnCharKey = null;
    }

    // ── INetworkClient ──────────────────────────────────────

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    // Connect / Disconnect の呼び出しは記録するだけで、接続状態はシナリオの connection ステップのみが変える（§5）。
    public void Connect(string url)
    {
    }

    public void Disconnect()
    {
    }

    public void Send(string type, object payload) => _sent.Add((type, payload));

    public event Action<string>? OnReceiveRaw;

    public event Action<ConnectionState, string?>? OnConnectionChanged;

    // ── IInputSource ─────────────────────────────────────────

    public event Action<char>? OnCharKey;

    // ── IClock ───────────────────────────────────────────────

    // 実時間を一切待たない。AdvanceTo でのみ進む（§5）。
    public long MonotonicMs => CurrentMs;

    public long WallClockUnixMs => WallClockBaseUnixMs + CurrentMs;
}

using System.Collections.Generic;
using Takoda99.Proto;

namespace Takoda99.Client.State;

public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting, Failed }

public enum ClientPhase { Boot, Title, Connecting, Matchmaking, InMatch, Spectating, Result }

/// <summary>行列の1エントリ。CustomerView は Proto DTO をそのまま保持する（加工しない）。</summary>
public sealed class CustomerEntry
{
    public CustomerView View { get; init; } = new();

    /// <summary>表示専用カウントダウンの基準となるローカル受信時刻（単調ms）。</summary>
    public long ArrivedAtLocalMs { get; init; }
}

/// <summary>現在の注文（表示専用ローカル値）。</summary>
public sealed class CurrentOrder
{
    public string CustomerId { get; init; } = "";
    public int WordIndex { get; init; }   // x
    public int OrderCount { get; init; }  // N
    public int TypedLength { get; init; }
    public int MissCount { get; init; }
    public long StartedAtMs { get; init; }
}

/// <summary>強制下位淘汰（storm）の警告。Proto の ForcedEliminationWarning から作るローカル表示用の形。</summary>
public sealed class StormWarning
{
    public int UntilTick { get; init; }
    public double ThresholdPct { get; init; }
}

/// <summary>試合結果。Proto の MatchEnd から作るローカル表示用の形。</summary>
public sealed class MatchResult
{
    public int FinalRank { get; init; }
    public MatchStats Stats { get; init; } = new();
}

/// <summary>イベントログの1行（デバッグパネル・演出トリガー用）。</summary>
public sealed class LogEntry
{
    public string Message { get; init; } = "";
    public long AtMs { get; init; }
}

/// <summary>クライアントの可変状態すべて。第4章 §2 の形に対応する。</summary>
public sealed class ClientState
{
    // 接続・ライフサイクル
    public ConnectionState Connection { get; init; }
    public ClientPhase Phase { get; init; }
    public string? LastError { get; init; }

    // マッチング
    public int WaitingCount { get; init; }
    public int MinPlayers { get; init; }
    public int? CountdownMs { get; init; }
    public IReadOnlyList<MatchmakingParticipant> MatchmakingParticipants { get; init; } = System.Array.Empty<MatchmakingParticipant>();

    // 試合の同定・公開パラメータ
    public string MatchId { get; init; } = "";
    public string SelfStoreId { get; init; } = "";
    public GameParametersPublicSubset Params { get; init; } = new();
    public Phase MatchPhase { get; init; }
    public long StartedAtMs { get; init; }

    // 自店（すべて受信値。自前算出しない）
    public int CreditLife { get; init; }
    public double EvalRaw { get; init; }
    public double Normalized { get; init; }
    public int Rank { get; init; }
    public int AliveCount { get; init; }

    /// <summary>表示専用の星（0..5）。EvaluationUpdate の受信値そのまま。Normalized とは別物で、再計算しない。</summary>
    public double StarRating { get; init; }

    /// <summary>前ティックからの星の増減。受信値そのまま。</summary>
    public double StarDelta { get; init; }

    public int HeatLevel { get; init; }
    public bool Alive { get; init; }

    public IReadOnlyList<CustomerEntry> Queue { get; init; } = System.Array.Empty<CustomerEntry>();        // 先頭＝対応中
    public CurrentOrder? CurrentOrder { get; init; }
    public IReadOnlyList<StoreSummary> Stores { get; init; } = System.Array.Empty<StoreSummary>();        // 99店概況
    public StormWarning? Storm { get; init; }
    public MatchResult? Result { get; init; }
    public IReadOnlyList<LogEntry> EventLog { get; init; } = System.Array.Empty<LogEntry>();

    /// <summary>
    /// 一部フィールドだけを差し替えた新しいインスタンスを作る（イミュータブル更新のための内部ヘルパー）。
    /// 値型は Nullable でラップし、未指定（null）なら既存値を維持する。参照型は既存値と同一の場合は
    /// 素通ししてよい（呼び出し側が変更したい値のみ渡す）。
    /// </summary>
    internal ClientState With(
        ConnectionState? connection = null,
        ClientPhase? phase = null,
        string? lastError = null,
        bool clearLastError = false,
        int? waitingCount = null,
        int? minPlayers = null,
        int? countdownMs = null,
        bool clearCountdownMs = false,
        IReadOnlyList<MatchmakingParticipant>? matchmakingParticipants = null,
        string? matchId = null,
        string? selfStoreId = null,
        GameParametersPublicSubset? gameParams = null,
        Phase? matchPhase = null,
        long? startedAtMs = null,
        int? creditLife = null,
        double? evalRaw = null,
        double? normalized = null,
        int? rank = null,
        int? aliveCount = null,
        double? starRating = null,
        double? starDelta = null,
        int? heatLevel = null,
        bool? alive = null,
        IReadOnlyList<CustomerEntry>? queue = null,
        CurrentOrder? currentOrder = null,
        bool clearCurrentOrder = false,
        IReadOnlyList<StoreSummary>? stores = null,
        StormWarning? storm = null,
        MatchResult? result = null,
        IReadOnlyList<LogEntry>? eventLog = null)
    {
        return new ClientState
        {
            Connection = connection ?? Connection,
            Phase = phase ?? Phase,
            LastError = clearLastError ? null : (lastError ?? LastError),
            WaitingCount = waitingCount ?? WaitingCount,
            MinPlayers = minPlayers ?? MinPlayers,
            CountdownMs = clearCountdownMs ? null : (countdownMs ?? CountdownMs),
            MatchmakingParticipants = matchmakingParticipants ?? MatchmakingParticipants,
            MatchId = matchId ?? MatchId,
            SelfStoreId = selfStoreId ?? SelfStoreId,
            Params = gameParams ?? Params,
            MatchPhase = matchPhase ?? MatchPhase,
            StartedAtMs = startedAtMs ?? StartedAtMs,
            CreditLife = creditLife ?? CreditLife,
            EvalRaw = evalRaw ?? EvalRaw,
            Normalized = normalized ?? Normalized,
            Rank = rank ?? Rank,
            AliveCount = aliveCount ?? AliveCount,
            StarRating = starRating ?? StarRating,
            StarDelta = starDelta ?? StarDelta,
            HeatLevel = heatLevel ?? HeatLevel,
            Alive = alive ?? Alive,
            Queue = queue ?? Queue,
            CurrentOrder = clearCurrentOrder ? null : (currentOrder ?? CurrentOrder),
            Stores = stores ?? Stores,
            Storm = storm ?? Storm,
            Result = result ?? Result,
            EventLog = eventLog ?? EventLog,
        };
    }
}

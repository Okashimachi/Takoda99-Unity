using System.Collections.Generic;
using Takoda99.Client.Typing;
using Takoda99.Proto;

namespace Takoda99.Client.State;

/// <summary>すべての状態更新はこの型を通す。1 S2C メッセージ ＝ 1 Action。</summary>
public interface IAction
{
}

// ── S2C Action（04-store-reducer.md §3.1） ──────────────────────────

public sealed class MatchStartAction : IAction
{
    public string MatchId { get; init; } = "";
    public string SelfStoreId { get; init; } = "";
    public GameParametersPublicSubset Params { get; init; } = new();
    public Phase MatchPhase { get; init; }
    public IReadOnlyList<StoreSummary> Stores { get; init; } = System.Array.Empty<StoreSummary>();
}

public sealed class CustomerArrivedAction : IAction
{
    public CustomerView Customer { get; init; } = new();
    public long ArrivedAtLocalMs { get; init; }
}

public sealed class CustomerLeftAction : IAction
{
    public string CustomerId { get; init; } = "";
    public LeaveReason Reason { get; init; }
}

public sealed class CreditUpdateAction : IAction
{
    public int Life { get; init; }
    public CreditReason Reason { get; init; }
}

public sealed class EvaluationUpdateAction : IAction
{
    public double EvalRaw { get; init; }
    public double Normalized { get; init; }
    public int Rank { get; init; }
    public int AliveCount { get; init; }

    /// <summary>表示専用の星（0..5）。サーバー値をそのまま運ぶ（クライアントで算出しない）。</summary>
    public double StarRating { get; init; }

    /// <summary>前ティックからの星の増減。0 なら増減演出を出さない。</summary>
    public double StarDelta { get; init; }
}

public sealed class DifficultyUpdateAction : IAction
{
    public int HeatLevel { get; init; }
}

public sealed class PhaseChangeAction : IAction
{
    public Phase Phase { get; init; }
}

public sealed class StoreListUpdateAction : IAction
{
    public IReadOnlyList<StoreSummary> Stores { get; init; } = System.Array.Empty<StoreSummary>();
    public int AliveCount { get; init; }
}

public sealed class ForcedEliminationWarningAction : IAction
{
    public int UntilTick { get; init; }
    public double ThresholdPct { get; init; }
}

public sealed class StoreEliminatedAction : IAction
{
    public string StoreId { get; init; } = "";
    public EliminationReason Reason { get; init; }
    public int FinalRank { get; init; }
}

public sealed class MatchEndAction : IAction
{
    public int FinalRank { get; init; }
    public MatchStats Stats { get; init; } = new();
}

public sealed class MatchmakingStatusAction : IAction
{
    public int WaitingCount { get; init; }
    public int MinPlayers { get; init; }
    public int? CountdownMs { get; init; }
    public string SelfStoreId { get; init; } = "";
    public IReadOnlyList<MatchmakingParticipant> Participants { get; init; } = System.Array.Empty<MatchmakingParticipant>();
}

// ── ローカル Action（04-store-reducer.md §3.2） ─────────────────────

public sealed class LocalOrderBeganAction : IAction
{
    public LocalOrderBeganAction(string customerId, int orderCount)
    {
        CustomerId = customerId;
        OrderCount = orderCount;
    }

    public string CustomerId { get; }
    public int OrderCount { get; }
}

public sealed class LocalKeyJudgedAction : IAction
{
    public LocalKeyJudgedAction(KeyResult result, TypingView view)
    {
        Result = result;
        View = view;
    }

    public KeyResult Result { get; }
    public TypingView View { get; }
}

public sealed class LocalOrderClearedAction : IAction
{
    public LocalOrderClearedAction(string customerId)
    {
        CustomerId = customerId;
    }

    public string CustomerId { get; }
}

public sealed class LocalConnectionChangedAction : IAction
{
    public LocalConnectionChangedAction(ConnectionState state, string? error)
    {
        State = state;
        Error = error;
    }

    public ConnectionState State { get; }
    public string? Error { get; }
}

public sealed class LocalLifecycleChangedAction : IAction
{
    public LocalLifecycleChangedAction(ClientPhase phase)
    {
        Phase = phase;
    }

    public ClientPhase Phase { get; }
}

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

    /// <summary>Dispatcher が IClock.MonotonicMs を入れる。MatchStart.StartsAtServerMs は使わない
    /// （ローカル補間の基準はローカル単調時計で揃える）。</summary>
    public long StartedAtLocalMs { get; init; }
}

public sealed class CustomerArrivedAction : IAction
{
    public CustomerView Customer { get; init; } = new();
    public long ArrivedAtLocalMs { get; init; }
}

public sealed class EvaluationUpdateAction : IAction
{
    public int Score { get; init; }
    public int Rank { get; init; }
    public int AliveCount { get; init; }
}

public sealed class DifficultyUpdateAction : IAction
{
    public int HeatLevel { get; init; }
}

public sealed class PhaseChangeAction : IAction
{
    public Phase Phase { get; init; }
}

/// <summary>全店ランキングの全量配信（match-state/02 §3.2）。</summary>
public sealed class RankingSnapshotAction : IAction
{
    /// <summary>Dispatcher が null を空リストへ正規化して渡す。</summary>
    public IReadOnlyList<RankingEntry> Entries { get; init; } = System.Array.Empty<RankingEntry>();
}

/// <summary>変化した店のみの差分配信（match-state/02 §3.3）。RankingChange は rank を持たない。</summary>
public sealed class RankingDeltaAction : IAction
{
    public IReadOnlyList<RankingChange> Entries { get; init; } = System.Array.Empty<RankingChange>();
}

public sealed class ForcedEliminationWarningAction : IAction
{
    public int UntilMs { get; init; }

    /// <summary>Dispatcher が IClock.MonotonicMs を入れる（補間の起点）。</summary>
    public long ReceivedAtLocalMs { get; init; }

    public int StageIndex { get; init; }
    public int StageTotal { get; init; }
    public int CutLineRank { get; init; }
    public IReadOnlyList<string> CutStoreIds { get; init; } = System.Array.Empty<string>();
    public bool SelfAtRisk { get; init; }
}

/// <summary>1回の足切りで脱落した店のまとめ（match-state/03 §4）。1件ずつ Apply しない。</summary>
public sealed class StoreEliminatedBatchAction : IAction
{
    public int StageIndex { get; init; }
    public IReadOnlyList<StoreEliminated> Entries { get; init; } = System.Array.Empty<StoreEliminated>();
}

/// <summary>自店の脱落確定と同時に届く個人成績（result/01 §3.1）。Phase を変えない。</summary>
public sealed class PersonalResultAction : IAction
{
    public int FinalRank { get; init; }
    public int Score { get; init; }
    public int TakoyakiCount { get; init; }
    public long SurvivedMs { get; init; }
    public MatchStats Stats { get; init; } = new();
}

/// <summary>ペイロードを持たない（Proto v0.8.0 の MatchEnd は空クラス）。</summary>
public sealed class MatchEndAction : IAction
{
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

/// <summary>
/// 試合に紐づくローカル保持値をすべて捨てる（result/01 §4）。再戦・タイトル復帰の入口で1回だけ呼ぶ。
/// Connection / Phase / LastError / EventLog は触らない（呼び出し側のライフサイクル管理に属する）。
/// </summary>
public sealed class LocalMatchResetAction : IAction
{
}

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

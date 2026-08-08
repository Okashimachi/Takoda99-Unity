using System.Collections.Generic;
using System.Linq;
using Takoda99.Proto;

namespace Takoda99.Client.State;

/// <summary>純粋関数。同じ (state, action) からは常に同じ結果を返す（04-store-reducer.md）。</summary>
public static class Reducer
{
    public static ClientState Apply(ClientState state, IAction action)
    {
        return action switch
        {
            MatchStartAction a => ApplyMatchStart(state, a),
            CustomerArrivedAction a => ApplyCustomerArrived(state, a),
            CustomerLeftAction a => ApplyCustomerLeft(state, a),
            CreditUpdateAction a => state.With(creditLife: a.Life),
            EvaluationUpdateAction a => state.With(evalRaw: a.EvalRaw, normalized: a.Normalized, rank: a.Rank, aliveCount: a.AliveCount, starRating: a.StarRating, starDelta: a.StarDelta),
            DifficultyUpdateAction a => state.With(heatLevel: a.HeatLevel),
            PhaseChangeAction a => state.With(matchPhase: a.Phase),
            StoreListUpdateAction a => state.With(stores: a.Stores, aliveCount: a.AliveCount),
            ForcedEliminationWarningAction a => state.With(storm: new StormWarning { UntilTick = a.UntilTick, ThresholdPct = a.ThresholdPct }),
            StoreEliminatedAction a => ApplyStoreEliminated(state, a),
            MatchEndAction a => state.With(
                result: new MatchResult
                {
                    FinalRank = a.FinalRank,
                    Stats = a.Stats,
                    Reason = a.Reason,
                    MatchElapsedMs = a.MatchElapsedMs,
                    CreditLeft = a.CreditLeft,
                    EvalRaw = a.EvalRaw,
                    EvalNormalized = a.EvalNormalized,
                },
                phase: ClientPhase.Result),
            MatchmakingStatusAction a => state.With(waitingCount: a.WaitingCount, minPlayers: a.MinPlayers, countdownMs: a.CountdownMs, clearCountdownMs: a.CountdownMs is null, selfStoreId: a.SelfStoreId, matchmakingParticipants: a.Participants),

            LocalOrderBeganAction a => state.With(currentOrder: new CurrentOrder { CustomerId = a.CustomerId, OrderCount = a.OrderCount }),
            LocalKeyJudgedAction a => ApplyLocalKeyJudged(state, a),
            LocalOrderClearedAction => ApplyLocalOrderCleared(state),
            LocalConnectionChangedAction a => state.With(connection: a.State, lastError: a.Error, clearLastError: a.Error is null),
            LocalLifecycleChangedAction a => state.With(phase: a.Phase),

            _ => state,
        };
    }

    private static ClientState ApplyMatchStart(ClientState state, MatchStartAction a)
    {
        return state.With(
            matchId: a.MatchId,
            selfStoreId: a.SelfStoreId,
            gameParams: a.Params,
            matchPhase: a.MatchPhase,
            startedAtMs: state.StartedAtMs,
            creditLife: a.Params.InitialLife,
            stores: a.Stores,
            phase: ClientPhase.InMatch,
            queue: System.Array.Empty<CustomerEntry>());
    }

    private static ClientState ApplyCustomerArrived(ClientState state, CustomerArrivedAction a)
    {
        // 対応中／待機中を問わず、同一 customerId が既に行列にいれば後着を無視する（§3.4）。
        if (state.Queue.Any(e => e.View.CustomerId == a.Customer.CustomerId))
        {
            return state;
        }

        var queue = state.Queue
            .Append(new CustomerEntry { View = a.Customer, ArrivedAtLocalMs = a.ArrivedAtLocalMs })
            .ToList();

        return state.With(queue: queue);
    }

    private static ClientState ApplyCustomerLeft(ClientState state, CustomerLeftAction a)
    {
        var index = FindIndex(state.Queue, a.CustomerId);
        if (index < 0)
        {
            // 未知の customerId：無視して state 不変（§3.4）。
            return state;
        }

        var queue = state.Queue.Where((_, i) => i != index).ToList();
        var wasServing = index == 0;

        return state.With(
            queue: queue,
            currentOrder: wasServing ? null : state.CurrentOrder,
            clearCurrentOrder: wasServing);
    }

    private static ClientState ApplyStoreEliminated(ClientState state, StoreEliminatedAction a)
    {
        var stores = state.Stores
            .Select(s => s.StoreId == a.StoreId ? CloneWithAlive(s, false, a.FinalRank) : s)
            .ToList();

        if (a.StoreId == state.SelfStoreId)
        {
            return state.With(
                stores: stores,
                alive: false,
                phase: ClientPhase.Spectating,
                clearCurrentOrder: true,
                queue: System.Array.Empty<CustomerEntry>());
        }

        return state.With(stores: stores);
    }

    private static ClientState ApplyLocalKeyJudged(ClientState state, LocalKeyJudgedAction a)
    {
        if (state.CurrentOrder is null)
        {
            return state;
        }

        var updated = new CurrentOrder
        {
            CustomerId = state.CurrentOrder.CustomerId,
            OrderCount = state.CurrentOrder.OrderCount,
            StartedAtMs = state.CurrentOrder.StartedAtMs,
            WordIndex = a.View.WordIndex,
            TypedLength = a.View.TypedKanaLength,
            MissCount = a.View.MissCount,
        };

        return state.With(currentOrder: updated);
    }

    private static ClientState ApplyLocalOrderCleared(ClientState state)
    {
        if (state.Queue.Count == 0)
        {
            return state.With(clearCurrentOrder: true);
        }

        return state.With(queue: state.Queue.Skip(1).ToList(), clearCurrentOrder: true);
    }

    private static int FindIndex(IReadOnlyList<CustomerEntry> queue, string customerId)
    {
        for (var i = 0; i < queue.Count; i++)
        {
            if (queue[i].View.CustomerId == customerId)
            {
                return i;
            }
        }

        return -1;
    }

    private static StoreSummary CloneWithAlive(StoreSummary source, bool alive, int finalRank)
    {
        return new StoreSummary
        {
            StoreId = source.StoreId,
            DisplayName = source.DisplayName,
            EvalNormalized = source.EvalNormalized,
            Rank = source.Rank,
            CreditLife = source.CreditLife,
            Alive = alive,
            FinalRank = finalRank,
        };
    }
}

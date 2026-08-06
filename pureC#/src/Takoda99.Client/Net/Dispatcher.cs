using System;
using System.Collections.Generic;
using Takoda99.Client.Contract;
using Takoda99.Client.State;
using Takoda99.Proto;

namespace Takoda99.Client.Net;

/// <summary><see cref="IDispatcher"/> の実装（05-dispatcher.md）。</summary>
public sealed class Dispatcher : IDispatcher
{
    private static readonly IReadOnlyDictionary<string, HashSet<ClientPhase>> AcceptedPhases =
        new Dictionary<string, HashSet<ClientPhase>>
        {
            [MessageType.MatchmakingStatus] = new() { ClientPhase.Connecting, ClientPhase.Matchmaking },
            [MessageType.MatchStart] = new() { ClientPhase.Matchmaking },
            [MessageType.CustomerArrived] = new() { ClientPhase.InMatch },
            [MessageType.CustomerLeft] = new() { ClientPhase.InMatch },
            [MessageType.CreditUpdate] = new() { ClientPhase.InMatch },
            [MessageType.EvaluationUpdate] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.DifficultyUpdate] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.PhaseChange] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.StoreListUpdate] = new() { ClientPhase.Matchmaking, ClientPhase.InMatch, ClientPhase.Spectating, ClientPhase.Result },
            [MessageType.ForcedEliminationWarning] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.StoreEliminated] = new() { ClientPhase.InMatch, ClientPhase.Spectating, ClientPhase.Result },
            [MessageType.MatchEnd] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
        };

    private readonly IEnvelopeCodec _codec;
    private readonly IStore _store;
    private readonly IEnvelopeLog _log;
    private readonly IClock _clock;

    public Dispatcher(IEnvelopeCodec codec, IStore store, IEnvelopeLog log, IClock clock)
    {
        _codec = codec;
        _store = store;
        _log = log;
        _clock = clock;
    }

    public event Action<string, string>? OnUnknownMessage;
    public event Action<string, string>? OnMessageDropped;
    public event Action<IAction>? OnActionApplied;

    public void HandleRaw(string json)
    {
        _log.RecordIncoming(json);

        var envelope = _codec.DecodeEnvelope(json);
        if (envelope is null)
        {
            OnMessageDropped?.Invoke("", "decode-failed");
            return;
        }

        if (!AcceptedPhases.TryGetValue(envelope.Type, out var allowedPhases))
        {
            OnUnknownMessage?.Invoke(envelope.Type, "unknown-type");
            return;
        }

        if (!allowedPhases.Contains(_store.State.Phase))
        {
            OnMessageDropped?.Invoke(envelope.Type, "phase-not-allowed");
            return;
        }

        var action = Decode(envelope);
        if (action is null)
        {
            OnMessageDropped?.Invoke(envelope.Type, "payload-decode-failed");
            return;
        }

        _store.Apply(action);
        OnActionApplied?.Invoke(action);
    }

    private IAction? Decode(Envelope envelope)
    {
        switch (envelope.Type)
        {
            case MessageType.MatchmakingStatus:
                var matchmakingStatus = _codec.DecodePayload<MatchmakingStatus>(envelope);
                return matchmakingStatus is null
                    ? null
                    : new MatchmakingStatusAction
                    {
                        WaitingCount = matchmakingStatus.WaitingCount,
                        MinPlayers = matchmakingStatus.MinPlayers,
                        CountdownMs = matchmakingStatus.CountdownMs,
                        SelfStoreId = matchmakingStatus.SelfStoreId,
                        Participants = matchmakingStatus.Participants,
                    };

            case MessageType.MatchStart:
                var matchStart = _codec.DecodePayload<MatchStart>(envelope);
                return matchStart is null
                    ? null
                    : new MatchStartAction
                    {
                        MatchId = matchStart.MatchId,
                        SelfStoreId = matchStart.SelfStoreId,
                        Params = matchStart.Params,
                        MatchPhase = matchStart.Phase,
                        Stores = matchStart.Stores,
                    };

            case MessageType.CustomerArrived:
                var customer = _codec.DecodePayload<CustomerView>(envelope);
                return customer is null
                    ? null
                    : new CustomerArrivedAction { Customer = customer, ArrivedAtLocalMs = _clock.MonotonicMs };

            case MessageType.CustomerLeft:
                var customerLeft = _codec.DecodePayload<CustomerLeft>(envelope);
                return customerLeft is null
                    ? null
                    : new CustomerLeftAction { CustomerId = customerLeft.CustomerId, Reason = customerLeft.Reason };

            case MessageType.CreditUpdate:
                var creditUpdate = _codec.DecodePayload<CreditUpdate>(envelope);
                return creditUpdate is null
                    ? null
                    : new CreditUpdateAction { Life = creditUpdate.Life, Reason = creditUpdate.Reason };

            case MessageType.EvaluationUpdate:
                var evaluationUpdate = _codec.DecodePayload<EvaluationUpdate>(envelope);
                return evaluationUpdate is null
                    ? null
                    : new EvaluationUpdateAction
                    {
                        EvalRaw = evaluationUpdate.EvalRaw,
                        Normalized = evaluationUpdate.Normalized,
                        Rank = evaluationUpdate.Rank,
                        AliveCount = evaluationUpdate.AliveCount,
                    };

            case MessageType.DifficultyUpdate:
                var difficultyUpdate = _codec.DecodePayload<DifficultyUpdate>(envelope);
                return difficultyUpdate is null
                    ? null
                    : new DifficultyUpdateAction { HeatLevel = difficultyUpdate.HeatLevel };

            case MessageType.PhaseChange:
                var phaseChange = _codec.DecodePayload<PhaseChange>(envelope);
                return phaseChange is null
                    ? null
                    : new PhaseChangeAction { Phase = phaseChange.Phase };

            case MessageType.StoreListUpdate:
                var storeListUpdate = _codec.DecodePayload<StoreListUpdate>(envelope);
                return storeListUpdate is null
                    ? null
                    : new StoreListUpdateAction { Stores = storeListUpdate.Stores, AliveCount = storeListUpdate.AliveCount };

            case MessageType.ForcedEliminationWarning:
                var warning = _codec.DecodePayload<ForcedEliminationWarning>(envelope);
                return warning is null
                    ? null
                    : new ForcedEliminationWarningAction { UntilTick = warning.UntilTick, ThresholdPct = warning.ThresholdPct };

            case MessageType.StoreEliminated:
                var eliminated = _codec.DecodePayload<StoreEliminated>(envelope);
                return eliminated is null
                    ? null
                    : new StoreEliminatedAction { StoreId = eliminated.StoreId, Reason = eliminated.Reason, FinalRank = eliminated.FinalRank };

            case MessageType.MatchEnd:
                var matchEnd = _codec.DecodePayload<MatchEnd>(envelope);
                return matchEnd is null
                    ? null
                    : new MatchEndAction { FinalRank = matchEnd.FinalRank, Stats = matchEnd.Stats };

            default:
                return null;
        }
    }
}

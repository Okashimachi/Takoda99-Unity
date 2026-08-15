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
            [MessageType.EvaluationUpdate] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.DifficultyUpdate] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.PhaseChange] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            // Result でも全量を受けるのは、120秒の配信順序が
            // StoreEliminatedBatch → PersonalResult → RankingSnapshot → MatchEnd であり、
            // 最後のスナップショット（＝全店の最終順位）をリザルト画面が使うため。
            [MessageType.RankingSnapshot] = new() { ClientPhase.InMatch, ClientPhase.Spectating, ClientPhase.Result },
            [MessageType.RankingDelta] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.ForcedEliminationWarning] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.StoreEliminatedBatch] = new() { ClientPhase.InMatch, ClientPhase.Spectating, ClientPhase.Result },
            [MessageType.PersonalResult] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
            [MessageType.MatchEnd] = new() { ClientPhase.InMatch, ClientPhase.Spectating },
        };

    /// <summary>
    /// Proto v0.8.0 では List 型のフィールドが null で届き得る。ClientState に null を入れないため
    /// Decode の時点で空リストへ正規化する（contract/01 §5）。
    /// </summary>
    private static IReadOnlyList<T> OrEmpty<T>(List<T>? source)
        => source ?? (IReadOnlyList<T>)Array.Empty<T>();

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
                        Stores = OrEmpty(matchStart.Stores),
                        StartedAtLocalMs = _clock.MonotonicMs,
                    };

            case MessageType.CustomerArrived:
                var customer = _codec.DecodePayload<CustomerView>(envelope);
                return customer is null
                    ? null
                    : new CustomerArrivedAction { Customer = customer, ArrivedAtLocalMs = _clock.MonotonicMs };

            case MessageType.EvaluationUpdate:
                var evaluationUpdate = _codec.DecodePayload<EvaluationUpdate>(envelope);
                // evalRaw / normalized / starRating / starDelta は Obsolete（0 が届く）。読まない。
                return evaluationUpdate is null
                    ? null
                    : new EvaluationUpdateAction
                    {
                        Score = evaluationUpdate.Score,
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

            case MessageType.RankingSnapshot:
                var rankingSnapshot = _codec.DecodePayload<RankingSnapshot>(envelope);
                return rankingSnapshot is null
                    ? null
                    : new RankingSnapshotAction { Entries = OrEmpty(rankingSnapshot.Entries) };

            case MessageType.RankingDelta:
                var rankingDelta = _codec.DecodePayload<RankingDelta>(envelope);
                return rankingDelta is null
                    ? null
                    : new RankingDeltaAction { Entries = OrEmpty(rankingDelta.Entries) };

            case MessageType.ForcedEliminationWarning:
                var warning = _codec.DecodePayload<ForcedEliminationWarning>(envelope);
                // untilTick / thresholdPct は Obsolete。読まない。
                // ReceivedAtLocalMs は「その予告を受け取った瞬間」でなければならないため、
                // 純関数の Reducer ではなくここで時刻を取る（CustomerArrivedAction と同じ方式）。
                return warning is null
                    ? null
                    : new ForcedEliminationWarningAction
                    {
                        UntilMs = warning.UntilMs,
                        ReceivedAtLocalMs = _clock.MonotonicMs,
                        StageIndex = warning.StageIndex,
                        StageTotal = warning.StageTotal,
                        CutLineRank = warning.CutLineRank,
                        CutStoreIds = OrEmpty(warning.CutStoreIds),
                        SelfAtRisk = warning.SelfAtRisk,
                    };

            case MessageType.StoreEliminatedBatch:
                var batch = _codec.DecodePayload<StoreEliminatedBatch>(envelope);
                return batch is null
                    ? null
                    : new StoreEliminatedBatchAction { StageIndex = batch.StageIndex, Entries = OrEmpty(batch.Entries) };

            case MessageType.PersonalResult:
                var personalResult = _codec.DecodePayload<PersonalResult>(envelope);
                // reason / creditLeft / 評価まわりの4フィールドは Obsolete。読まない。
                return personalResult is null
                    ? null
                    : new PersonalResultAction
                    {
                        FinalRank = personalResult.FinalRank,
                        Score = personalResult.Score,
                        TakoyakiCount = personalResult.TakoyakiCount,
                        SurvivedMs = personalResult.SurvivedMs,
                        Stats = personalResult.Stats ?? new MatchStats(),
                    };

            case MessageType.MatchEnd:
                // ペイロードは空クラス。`{}` でも decode が成功する。
                var matchEnd = _codec.DecodePayload<MatchEnd>(envelope);
                return matchEnd is null ? null : new MatchEndAction();

            default:
                return null;
        }
    }
}

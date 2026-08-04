using System.Collections.Generic;
using Takoda99.Client.Lifecycle;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;

namespace Takoda99.Client.Tests.Lifecycle;

public sealed class FakeRenderer : IRenderer
{
    public List<CustomerView> Arrived { get; } = new();
    public List<(string CustomerId, LeaveReason Reason)> Left { get; } = new();
    public List<KeyResult> KeyFeedback { get; } = new();
    public List<string> OrderServed { get; } = new();
    public List<Phase> PhaseChanges { get; } = new();
    public List<(string StoreId, EliminationReason Reason, int FinalRank)> StoreEliminations { get; } = new();
    public List<(int FinalRank, MatchStats Stats)> MatchEnds { get; } = new();
    public List<(ClientPhase From, ClientPhase To)> LifecycleChanges { get; } = new();
    public List<string> ConnectionTroubles { get; } = new();

    public void OnCustomerArrived(CustomerView customer) => Arrived.Add(customer);
    public void OnCustomerLeft(string customerId, LeaveReason reason) => Left.Add((customerId, reason));
    public void OnKeyFeedback(KeyResult result) => KeyFeedback.Add(result);
    public void OnOrderServed(string customerId) => OrderServed.Add(customerId);
    public void OnPhaseChanged(Phase phase) => PhaseChanges.Add(phase);
    public void OnForcedEliminationWarning(int untilTick, double thresholdPct)
    {
    }

    public void OnStoreEliminated(string storeId, EliminationReason reason, int finalRank) => StoreEliminations.Add((storeId, reason, finalRank));
    public void OnMatchEnd(int finalRank, MatchStats stats) => MatchEnds.Add((finalRank, stats));
    public void OnLifecycleChanged(ClientPhase from, ClientPhase to) => LifecycleChanges.Add((from, to));
    public void OnConnectionTrouble(string kind) => ConnectionTroubles.Add(kind);
}

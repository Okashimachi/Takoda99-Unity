using System.Collections.Generic;
using Takoda99.Client.Lifecycle;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;

namespace Takoda99.Client.Tests.Lifecycle;

public sealed class FakeRenderer : IRenderer
{
    public List<CustomerView> Arrived { get; } = new();
    public List<KeyResult> KeyFeedback { get; } = new();
    public List<string> OrderServed { get; } = new();
    public List<Phase> PhaseChanges { get; } = new();
    public List<CullWarning> CullWarnings { get; } = new();
    public List<(int StageIndex, IReadOnlyList<StoreEliminated> Entries, bool IncludesSelf)> EliminatedBatches { get; } = new();
    public List<PersonalResultState> PersonalResults { get; } = new();

    /// <summary>OnMatchEnd は引数を持たないため、呼ばれた回数だけを記録する。</summary>
    public int MatchEndCount { get; private set; }

    public List<(ClientPhase From, ClientPhase To)> LifecycleChanges { get; } = new();
    public List<string> ConnectionTroubles { get; } = new();

    public void OnCustomerArrived(CustomerView customer) => Arrived.Add(customer);
    public void OnKeyFeedback(KeyResult result) => KeyFeedback.Add(result);
    public void OnOrderServed(string customerId) => OrderServed.Add(customerId);
    public void OnPhaseChanged(Phase phase) => PhaseChanges.Add(phase);
    public void OnCullWarning(CullWarning warning) => CullWarnings.Add(warning);

    public void OnStoreEliminatedBatch(int stageIndex, IReadOnlyList<StoreEliminated> entries, bool includesSelf)
        => EliminatedBatches.Add((stageIndex, entries, includesSelf));

    public void OnPersonalResult(PersonalResultState result) => PersonalResults.Add(result);
    public void OnMatchEnd() => MatchEndCount++;
    public void OnLifecycleChanged(ClientPhase from, ClientPhase to) => LifecycleChanges.Add((from, to));
    public void OnConnectionTrouble(string kind) => ConnectionTroubles.Add(kind);
}

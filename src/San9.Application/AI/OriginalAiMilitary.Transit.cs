using San9.Core.Domain;

namespace San9.Application.AI;

internal sealed partial class OriginalAiMilitary
{
    // Potential reinforcement is incoming, not the target's outgoing travel scope.
    // Include the target itself once; army readiness and diplomacy belong to callers.
    private IEnumerable<SettlementState> ReinforcementArea(int target)
    {
        var ids = Transit.To(target).Select(r => r.SourceId).Append(target).ToHashSet();
        return World.Settlements.Where(s => ids.Contains(s.Id));
    }

    // Field armies are not graph nodes. Only the source region and regions hosting
    // directly connected endpoints qualify; never expand through a parent city or
    // intermediate route regions. Final interception uses the army's real position.
    private HashSet<int> InterceptionRegions(SettlementState source)
    {
        var network = Transit;
        var ids = network.From(source.Id).Select(r => r.Reference.TargetId).Append(source.Id).ToHashSet();
        return network.Nodes.Where(n => ids.Contains(n.Id))
            .Select(n => rules.RegionAt(n.Area.Position)).OfType<int>().ToHashSet();
    }
}

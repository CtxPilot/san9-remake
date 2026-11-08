using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

internal static class UnitDefeatTestHelper
{
    internal static void Complete(WorldMutation world, int id, BattleSource? winner, int? city,
        GovernanceRulebook governance, List<DomainEvent> events)
    {
        if (!world.Units.ContainsKey(id) || world.Officers.Values.Any(p => p.UnitId == id))
            throw new InvalidOperationException("Fixture personnel must be resolved before termination.");
        var settlement = UnitTerminationService.SettleResources(world, id, winner, city, events);
        UnitTerminationService.Complete(world, settlement, governance, events);
    }
}

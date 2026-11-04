using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

/// <summary>Strategic attack eligibility is distinct from Core's automatic battlefield hostility.</summary>
internal static class MilitaryCampaignPolicy
{
    internal static bool CanAttack(WorldSnapshot world, int force, int? owner) =>
        owner is null || OriginalAiStrategy.IsOpponent(world, force, owner.Value);

    internal static bool AllowsObjective(WorldSnapshot world, CorpsState corps, int objective)
    {
        var target = world.Settlements.FirstOrDefault(s => s.Id == objective && !s.IsDormant && !s.IsDestroyed);
        return target is not null && CanAttack(world, corps.OwnerId, target.OwnerId) && corps.Delegation switch
        {
            DomesticDelegation => false,
            DefeatForceDelegation directive => target.OwnerId == directive.ForceId,
            CaptureSettlementDelegation directive => target.Id == directive.SettlementId,
            _ => true
        };
    }

    internal static int? RequestedObjective(WorldSnapshot world, CorpsState corps) => world.Simulation.AttackRequests
        .Where(r => r.RequestedForceId == corps.OwnerId && r.RemainingXun > 0 &&
            world.Forces.Any(f => f.Id == r.RequesterForceId && !f.IsEliminated) &&
            AllowsObjective(world, corps, r.TargetSettlementId))
        .Select(r => (int?)r.TargetSettlementId).FirstOrDefault();

    internal static UnitPolicies CombatPolicies(ForceState force) => force.IsBarbarian
        ? new(true, true, true, false) : new(false, false, true, true);
}

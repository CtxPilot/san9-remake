using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Core.Simulation;

public sealed record DiplomaticConflictsEndedEvent(GameClock Clock, int FirstForceId, int SecondForceId,
    ImmutableArray<int> ReturningUnitIds, ImmutableArray<int> ClearedCorpsIds) : DomainEvent;

/// <summary>Ends existing bilateral attacks when improving relations removes automatic hostility.</summary>
internal static class DiplomaticConflicts
{
    private static bool Active(WorldMutation world, UnitState unit, MilitaryRulebook? military) => !unit.IsPersonnel &&
        (!unit.IsEmbedded || military is not null && unit.ServicePlaceId is int home && world.Settlements.TryGetValue(home, out var host) &&
            !host.IsDestroyed && host.Facility?.BuilderUnitId is null && !MilitaryDiplomacy.Work(world, home, military));

    private static MilitaryOrder? ReturnOrder(WorldMutation world, UnitState unit)
    {
        int? root = world.Corps.Values.SingleOrDefault(c => c.OwnerId == unit.OwnerId && c.IsRoot)?.Id;
        return unit.ServicePlaceId is int service && world.Settlements.TryGetValue(service, out var destination) &&
            UnitServicePlaceRules.IsEligible(unit, destination, root) ? new(MilitaryOrderKind.Return, null, service) : null;
    }

    private static bool CorpsTargets(WorldMutation world, CorpsState corps, int other) => !corps.IsRoot && corps.Delegation switch
    {
        DefeatForceDelegation defeat => defeat.ForceId == other,
        CaptureSettlementDelegation capture => world.Settlements.TryGetValue(capture.SettlementId, out var place) && place.OwnerId == other,
        _ => false
    };

    internal static (ImmutableArray<int> Units, ImmutableArray<int> Corps) Preview(WorldMutation world, int owner, int other, MilitaryRulebook? military) =>
        (world.OrderedUnits.Where(u => u.OwnerId == owner && Active(world, u, military) &&
             MilitaryDiplomacy.TargetOwner(world, u.Order) == other && ReturnOrder(world, u) is not null).Select(u => u.Id).ToImmutableArray(),
         world.Corps.Values.Where(c => c.OwnerId == owner && CorpsTargets(world, c, other)).OrderBy(c => c.Id).Select(c => c.Id).ToImmutableArray());

    internal static void End(WorldMutation world, int first, int second, MilitaryRulebook? military, List<DomainEvent> events)
    {
        List<int> returning = [], corpsIds = [];
        HashSet<int> releasedContacts = [];
        int? Other(int owner) => owner == first ? second : owner == second ? first : null;
        foreach (var place in world.Settlements.Values.ToArray())
            if (place.OwnerId is int owner && Other(owner) is int other && place.CombatTargetUnitId is int target &&
                (military is null || !MilitaryDiplomacy.Work(world, place.Id, military)) &&
                world.Units.TryGetValue(target, out var unit) && unit.OwnerId == other)
            {
                world.Replace(place with { CombatTargetUnitId = null });
                releasedContacts.Add(target);
            }
        foreach (var unit in world.OrderedUnits.ToArray())
        {
            if (!Active(world, unit, military) || Other(unit.OwnerId) is not int other) continue;
            bool attack = MilitaryDiplomacy.TargetOwner(world, unit.Order) == other;
            bool engagement = unit.EngagementTargetId is int engaged && world.Units.TryGetValue(engaged, out var target) && target.OwnerId == other;
            bool contact = unit.ContactTarget is { } current && (current.IsSettlement
                ? world.Settlements.TryGetValue(current.Id, out var place) && place.OwnerId == other
                : world.Units.TryGetValue(current.Id, out var field) && field.OwnerId == other);
            if (attack)
            {
                if (unit.ContactTarget is { IsSettlement: false } previous) releasedContacts.Add(previous.Id);
                var order = ReturnOrder(world, unit);
                MilitaryOrders.ChangeOrder(world, unit, order, events, "diplomacy-peace", autonomousReturn: true);
                world.Replace(world.Units[unit.Id] with { ActiveEngagement = false });
                if (order is not null) returning.Add(unit.Id);
                else events.Add(UnitPolicyRespondedEvent.Capture(world, world.Units[unit.Id], UnitPolicyResponse.ReturnHome,
                    null, null, returnOutcome: UnitPolicyReturnOutcome.NoDestination));
            }
            else if (engagement || contact)
            {
                if (unit.ContactTarget is { IsSettlement: false } previous) releasedContacts.Add(previous.Id);
                world.Replace(unit with { EngagementTargetId = engagement ? null : unit.EngagementTargetId,
                    ContactTarget = contact ? null : unit.ContactTarget, ActiveEngagement = false, Lookahead = null, MovementRemainder = 0 });
                if (engagement) events.Add(new UnitEngagementChangedEvent(world.Clock, unit.Id, null, "diplomacy-peace"));
            }
        }
        foreach (var corps in world.Corps.Values.OrderBy(c => c.Id).ToArray())
        {
            if (Other(corps.OwnerId) is not int other || !CorpsTargets(world, corps, other)) continue;
            CorpsDelegations.Change(world, corps, new GeneralDelegation(), "diplomacy-peace", events);
            corpsIds.Add(corps.Id);
        }
        // Remove only the passive contacts released here; ordinary expiry belongs to the combat phase.
        var retainedContacts = world.Units.Values.Where(u => u.ContactTarget is { IsSettlement: false })
            .Select(u => u.ContactTarget!.Id).Concat(world.Settlements.Values.Where(p => p.CombatTargetUnitId is not null)
                .Select(p => p.CombatTargetUnitId!.Value)).ToHashSet();
        foreach (int id in releasedContacts)
            if (!retainedContacts.Contains(id) && world.Units.TryGetValue(id, out var unit) && unit.PassiveEngagement)
                world.Replace(unit with { PassiveEngagement = false });
        if (returning.Count + corpsIds.Count > 0)
            events.Add(new DiplomaticConflictsEndedEvent(world.Clock, Math.Min(first, second), Math.Max(first, second),
                returning.ToImmutableArray(), corpsIds.ToImmutableArray()));
    }
}

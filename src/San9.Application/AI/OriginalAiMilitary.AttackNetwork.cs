using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI;

internal sealed partial class OriginalAiMilitary
{
    // Actual ordinary-sortie limit, independent of the reference graph's build horizon.
    private const int MaximumAttackTravelDays = 60;
    private readonly Dictionary<int, HashSet<int>> attackConnections = [];

    private HashSet<int> AttackConnections(SettlementState source)
    {
        Refresh();
        if (attackConnections.TryGetValue(source.Id, out var cached)) return cached;
        var traffic = terrainRoutes.TransitNetwork(rules, World);
        // The completed reference graph defines ordinary attack scope. Actual formations
        // may reject an in-scope route, but never introduce an out-of-scope target.
        var targets = traffic.From(source.Id).Select(connection => connection.Reference.TargetId).ToHashSet();
        attackConnections[source.Id] = targets;
        return targets;
    }

    private SettlementState? ResolveAttackTarget(SettlementState target, IEnumerable<SettlementState> origins)
    {
        var traffic = terrainRoutes.TransitNetwork(rules, World);
        var source = origins.FirstOrDefault(s => traffic.Find(s.Id, target.Id) is not null);
        if (source is null) return null;
        regionBuildings ??= World.Settlements.ToDictionary(s => s.Id);
        // The first intervening attackable settlement inherits the target's stage.
        // Resolve before strength/route rejection, while retaining the same physical source.
        foreach (int id in traffic.Find(source.Id, target.Id)!.Summary.ViaSettlementIds)
        {
            if (!regionBuildings.TryGetValue(id, out var building) || !Target(building) ||
                !rules.TryPlace(building, out var place) || place.FacilityTypeId is int type &&
                Policy.RouteObstacleFacilityIds.Contains(type)) continue;
            return traffic.Find(source.Id, building.Id) is not null ? building : null;
        }
        return target;
    }

    private Route? OrdinaryAttackRoute(SettlementState source, SettlementState target)
    {
        if (AttackFormation(source.Id) is not { } pair) return null;
        var connection = terrainRoutes.TransitNetwork(rules, World).Find(source.Id, target.Id);
        if (connection is null) return null;
        var summary = connection.Summary;
        var travel = rules.Travel(pair.Land, pair.Naval);
        if (travel.DailyBudget <= 0) return null;
        regionBuildings ??= World.Settlements.ToDictionary(s => s.Id);
        bool obstacle = summary.ViaSettlementIds.Prepend(source.Id).Append(target.Id)
            .Any(id => regionBuildings.TryGetValue(id, out var building) && rules.TryPlace(building, out var place) &&
                place.FacilityTypeId is int type && Policy.RouteObstacleFacilityIds.Contains(type));
        Route? route = new(summary.EstimateDays(travel), connection.Reference.Cells,
            summary.TerrainIds.Any(Policy.SlowTerrainIds.Contains), obstacle, World, rules, regionBuildings, Owner, target.Id)
            { TransitSummary = summary };
        // Ownership is current-world input. A blocked or unusable reference centerline may
        // have an alternative, but only this already in-scope target can be queried.
        if (route.HostileBuilding || pair.Naval is null && summary.TerrainCost.Water > 0)
            route = RouteFrom(source, pair.Land, pair.Naval,
                new(MilitaryOrderKind.AttackSettlement, null, target.Id), Policies());
        return route is { Days: <= MaximumAttackTravelDays, HostileBuilding: false } ? route : null;
    }

    private bool ConstructionConflict(int region) => MilitaryIndex.ConstructionRegions.Contains(region);

    private bool HasAttackConstructionConflict(SettlementState source, SettlementState target, Route route)
    {
        if (ConstructionConflict(target.Id)) return true;
        bool Conflicting(SettlementState building) => building.Id != source.Id && !building.IsDestroyed &&
            !building.IsDormant && rules.TryPlace(building, out var place) &&
            !(place.FacilityTypeId is int type && Policy.RouteObstacleFacilityIds.Contains(type)) &&
            ConstructionConflict(building.Id);
        // A construction destination in an empty region is not an intervening building.
        if (route.TransitSummary is { } summary)
        {
            regionBuildings ??= World.Settlements.ToDictionary(s => s.Id);
            return summary.ViaSettlementIds.Any(id => regionBuildings.TryGetValue(id, out var building) && Conflicting(building));
        }
        return route.Cells.Any(cell => BuildingAlongRoute(MapCoordinates.Center(cell), Conflicting) is not null);
    }

    private ImmutableArray<int> AttackRegions(SettlementState source, SettlementState target, Route route)
    {
        if (route.TransitSummary is { } summary) return summary.RegionIds;
        int origin = rules.RegionAt(rules.SettlementArea(source).Position) ?? source.Id;
        int destination = rules.RegionAt(rules.SettlementArea(target).Position) ?? target.Id;
        var regions = route.Cells.Select(c => rules.RegionAt(MapCoordinates.Center(c))).OfType<int>()
            .Prepend(origin).Append(destination).Distinct().ToImmutableArray();
        return regions;
    }

    private bool FrontContact(SettlementState target) => Geography.Neighbors(target.Id).Any(p => p.OwnerId == Owner);

    private bool SendOrdinaryAttack(DeployUnitCommand? command)
    {
        if (command is null) return Send(null);
        var source = World.Settlements.Single(s => s.Id == command.SettlementId);
        var start = Navigation.DeploymentPosition(source, command.FormationId, command.NavalFormationId,
            Owner, command.Policies, command.Soldiers);
        if (start is null) return Send(null);
        var unit = new UnitState(0, Owner, start.Value, command.Soldiers)
        {
            FormationId = command.FormationId, NavalFormationId = command.NavalFormationId,
            Order = command.Order, Policies = command.Policies
        };
        paths++;
        var estimate = Navigation.Estimate(unit, null);
        if (estimate.Status == RouteReachability.Incomplete) incomplete = true;
        if (estimate.Status != RouteReachability.Reachable || estimate.Days > MaximumAttackTravelDays ||
            estimate.Days is null) return Send(null);
        var buildings = World.Settlements.ToDictionary(s => s.Id);
        var route = new Route(estimate.Days.Value, estimate.Cells, false, false, World, rules,
            buildings, Owner, command.Order.SettlementId);
        if (route.HostileBuilding) return Send(null);
        return Send(command);
    }
}

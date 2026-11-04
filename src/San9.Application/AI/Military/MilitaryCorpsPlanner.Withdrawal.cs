using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

internal sealed partial class MilitaryCorpsPlanner
{
    private void ReviewCampaigns(MilitaryTravelQueries queries)
    {
        foreach (var campaign in batch.State.Campaigns.Where(c => c.ForceId == corps.OwnerId && c.CorpsId == corps.Id &&
            c.Purpose == MilitaryCampaignPurpose.Conquest && c.Stage == MilitaryCampaignStage.Marching).ToArray())
        {
            var target = queries.Observation.Forces.Find(new(campaign.TargetId, true));
            var living = campaign.UnitIds.Select(id => engine.Snapshot.Units.FirstOrDefault(u => u.Id == id && u.OwnerId == corps.OwnerId)).OfType<UnitState>().ToArray();
            if (target is null || target.OwnerId == corps.OwnerId || living.Length == 0) continue;
            long live = living.Sum(u => (long)u.Soldiers);
            long wallDamage = living.Where(u => u.FormationId is not null).Sum(u => (long)CombatMath.DurabilityDamage(u.Soldiers,
                u.CommanderId is int leader ? engine.GetAbilities(engine.Snapshot)[leader].Leadership : 40,
                rules.Formation(u.FormationId!.Value, u.NavalFormationId, target.Area.Position).Combat?.AgainstWalls ?? 0, target.Durability));
            bool imminent = target.Durability <= 2 * wallDamage && living.Any(u => rules.Travel(u).AreaAt(u.Position).DistanceTo(target.Area) <= 3);
            bool depleted = campaign.InitialSoldiers > 0 && live * 3 < campaign.InitialSoldiers;
            // Losses alone do not abandon a nearly completed siege. Changed opposition is re-observed before adding support.
            long opposition = batch.Battlefield(queries, rules, target.Object.Id, 10).PossibleAt(10);
            if (!imminent && depleted && opposition > live)
                Replace(campaign, campaign with { Stage = MilitaryCampaignStage.Withdrawing, Reservations = [], ReservedOfficers = [] });
        }
    }

    private void Withdraw(MilitaryCampaign campaign, MilitaryTravelQueries queries)
    {
        foreach (int unitId in campaign.UnitIds.ToArray())
        {
            var unit = engine.Snapshot.Units.FirstOrDefault(u => u.Id == unitId);
            if (unit is null || unit.IsEmbedded || !Controls(unit) || !unit.CanReceiveOrders || unit.CommandedThisXun) continue;
            var currentHome = engine.Snapshot.Settlements.FirstOrDefault(p => p.Id == unit.Order?.SettlementId && Friendly(p));
            if (unit.Order?.Kind == MilitaryOrderKind.Return && currentHome is not null &&
                (long)unit.Soldiers + unit.Wounded <= MilitaryResourceLedger.ReceivingSpace(engine.Snapshot, currentHome, unit.Id) &&
                queries.PotentialThreats(currentHome.Id, 10).Candidates.Sum(t => (long)t.Soldiers) <= currentHome.Soldiers) continue;
            bool returning = false;
            var candidates = engine.Snapshot.Settlements.Where(Friendly)
                .Where(p => (long)unit.Soldiers + unit.Wounded <= MilitaryResourceLedger.ReceivingSpace(engine.Snapshot, p, unit.Id))
                .OrderBy(p => rules.SettlementArea(p).DistanceTo(rules.Travel(unit).AreaAt(unit.Position)))
                .ThenBy(p => p.Id).Take(4).ToArray();
            foreach (var place in candidates)
            {
                var threats = queries.PotentialThreats(place.Id, 10).Candidates;
                if (threats.Sum(p => (long)p.Soldiers) > place.Soldiers + unit.Soldiers) continue;
                var arrival = queries.FromUnit(unit.Id, new(place.Id, true), arrival: NavigationArrival.Region, verifyApproach: true, newOrder: true);
                if (arrival.Status != ArrivalEstimateStatus.Known) continue;
                if (threats.Any(t => t.EarliestDays < arrival.Days && t.Soldiers > place.Soldiers)) continue;
                var id = Identity();
                if (Submit(new OrderUnitCommand(id.Id, corps.OwnerId, id.Sequence, unit.Id, new(MilitaryOrderKind.Return, null, place.Id))))
                { returning = true; break; }
            }
            if (!returning) Rally(unit, queries);
            queries = batch.Queries(engine.Snapshot, rules, corps.OwnerId);
        }
    }
    private void Rally(UnitState unit, MilitaryTravelQueries queries)
    {
        var current = rules.Travel(unit).AreaAt(unit.Position);
        var allies = queries.Observation.Forces.ForForce(corps.OwnerId).Where(p => p.Kind == MilitaryPresenceKind.Unit && p.Object.Id != unit.Id && p.Soldiers >= unit.Soldiers)
            .OrderBy(p => p.Area.DistanceTo(current)).ThenBy(p => p.Object.Id).Take(2);
        foreach (var ally in allies)
        foreach (var offset in new[] { new WorldPosition(-6, 0), new(0, -6), new(6, 0), new(0, 6) })
        {
            var point = new WorldPosition(ally.Area.Position.X + offset.X, ally.Area.Position.Y + offset.Y);
            if (point.X < 0 || point.Y < 0 || point.X >= rules.Map.FineWidth || point.Y >= rules.Map.FineHeight || point == unit.Position) continue;
            var area = rules.Travel(unit).AreaAt(point);
            var threats = queries.Observation.Forces.Nearby(area, 20).Objects.Where(p => p.Kind == MilitaryPresenceKind.Unit &&
                MilitaryDiplomacy.AutomaticEnemy(engine.Snapshot, corps.OwnerId, p.OwnerId)).ToArray();
            long enemies = threats.Sum(p => (long)p.Soldiers);
            if (enemies > (long)ally.Soldiers + unit.Soldiers || threats.Any(p => p.Area.DistanceTo(area) < p.Area.DistanceTo(current))) continue;
            if (unit.Order is { Kind: MilitaryOrderKind.Move } order && order.Position == point) return;
            if (queries.FromUnitToPosition(unit.Id, point, new(), verifyApproach: true).Status != ArrivalEstimateStatus.Known) continue;
            var id = Identity();
            if (Submit(new OrderUnitCommand(id.Id, corps.OwnerId, id.Sequence, unit.Id, new(MilitaryOrderKind.Move, point, null)) { Policies = new() })) return;
        }
        // If no legal safer endpoint is established, retain the current resistance rather than inventing an escape.
    }

}

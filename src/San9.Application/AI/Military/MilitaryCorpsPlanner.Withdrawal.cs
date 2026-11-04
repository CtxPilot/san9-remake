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
            if (unit.Order?.Kind == MilitaryOrderKind.Return) continue;
            var candidates = engine.Snapshot.Settlements.Where(Friendly)
                .Where(p => (long)unit.Soldiers + unit.Wounded <= MilitaryResourceLedger.ReceivingSpace(engine.Snapshot, p))
                .OrderBy(p => rules.SettlementArea(p).DistanceTo(rules.Travel(unit).AreaAt(unit.Position)))
                .ThenBy(p => p.Id).Take(4).ToArray();
            foreach (var place in candidates)
            {
                var threats = queries.PotentialThreats(place.Id, 10).Candidates;
                if (threats.Sum(p => (long)p.Soldiers) > place.Soldiers + unit.Soldiers) continue;
                var arrival = queries.FromUnit(unit.Id, new(place.Id, true), arrival: NavigationArrival.Region, verifyApproach: true);
                if (arrival.Status != ArrivalEstimateStatus.Known) continue;
                if (threats.Any(t => t.EarliestDays < arrival.Days && t.Soldiers > place.Soldiers)) continue;
                var id = Identity();
                if (Submit(new OrderUnitCommand(id.Id, corps.OwnerId, id.Sequence, unit.Id, new(MilitaryOrderKind.Return, null, place.Id)))) break;
            }
            queries = batch.Queries(engine.Snapshot, rules, corps.OwnerId);
        }
    }
}

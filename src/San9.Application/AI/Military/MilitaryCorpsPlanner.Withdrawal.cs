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
            var committed = CommittedForces(campaign, queries);
            long live = committed.TimelySoldiers;
            long wallDamage = committed.Participants.Where(p => p.Days <= committed.Window && p.Unit.FormationId is not null && p.Unit.EffectiveOrder is { Kind: MilitaryOrderKind.AttackSettlement }).Sum(p => (long)CombatMath.DurabilityDamage(p.Unit.Soldiers,
                p.Unit.CommanderId is int leader ? engine.GetAbilities(engine.Snapshot)[leader].Leadership : 40,
                rules.Formation(p.Unit.FormationId!.Value, p.Unit.NavalFormationId, target.Area.Position).Combat?.AgainstWalls ?? 0, target.Durability));
            long captureDays = CaptureDays(committed, target, wallDamage);
            bool imminent = captureDays <= 2 && committed.Participants.Any(p => p.Days <= 1 && p.Unit.EffectiveOrder is { Kind: MilitaryOrderKind.AttackSettlement });
            bool depleted = campaign.InitialSoldiers > 0 && living.Sum(u => (long)u.Soldiers) * 3 < campaign.InitialSoldiers;
            var battlefield = batch.Battlefield(queries, rules, target.Object.Id, committed.Window);
            long opposition = battlefield.PossibleAt(committed.Window);
            bool fieldResponse = battlefield.Contributions.Any(c => c.Kind == MilitaryContributionKind.FieldResponse &&
                c.Status is not (ArrivalEstimateStatus.Unreachable or ArrivalEstimateStatus.NotApplicable) && (c.ArrivalDay ?? c.EarliestDay) <= committed.Window);
            bool exposed = fieldResponse && SiegeExposed(campaign, committed, target, battlefield, queries, captureDays);
            if (committed.Uncertain) { incomplete = true; continue; }
            if (!imminent && (depleted && opposition > live || exposed))
            {
                var actual = batch.State.Campaigns.Single(c => c.Id == campaign.Id);
                Replace(actual, actual with { Stage = MilitaryCampaignStage.Withdrawing, Reservations = [], ReservedOfficers = [] });
                reason = MilitaryDecisionReason.Withdrawing;
            }
        }
    }

    private long CaptureDays(CampaignForces own, MilitaryPresence target, long wallDamage)
    {
        if (target.Soldiers == 0) return 1;
        long wall = wallDamage == 0 ? long.MaxValue : (target.Durability + wallDamage - 1) / wallDamage;
        var profile = target.FacilityTypeId is int facility ? rules.Facilities[facility].Combat : rules.Places.GetValueOrDefault(target.Object.Id)?.Combat;
        if (profile is null) return wall;
        long damage = own.Participants.Where(p => p.Days <= own.Window && p.Unit.FormationId is not null && p.Unit.EffectiveOrder is { Kind: MilitaryOrderKind.AttackSettlement }).Sum(p =>
        {
            var unit = p.Unit;
            var attack = rules.Formation(unit.FormationId!.Value, unit.NavalFormationId, target.Area.Position).Combat;
            if (attack is null) return 0L;
            int leadership = unit.CommanderId is int leader ? engine.GetAbilities(engine.Snapshot)[leader].Leadership : 40;
            return CombatMath.TroopDamage(new(unit.Soldiers, unit.Morale, leadership, 1, 10), new(target.Soldiers, target.Morale, 70, 1, 10), attack, profile, true, target.Durability);
        });
        return Math.Max(1, Math.Min(wall, damage == 0 ? long.MaxValue : (target.Soldiers + damage - 1) / damage));
    }

    private bool SiegeExposed(MilitaryCampaign campaign, CampaignForces own, MilitaryPresence target,
        MilitaryBattlefield battlefield, MilitaryTravelQueries queries, long captureDays)
    {
        var primary = own.Participants.FirstOrDefault(p => p.Unit.Id == campaign.UnitId && p.Days <= own.Window)?.Unit;
        if (primary?.FormationId is not int formation || rules.Formation(formation, primary.NavalFormationId, target.Area.Position).Combat is not { } defense) return false;
        int leadership = primary.CommanderId is int leader ? engine.GetAbilities(engine.Snapshot)[leader].Leadership : 40;
        var defender = new CombatStrength(primary.Soldiers, primary.Morale, leadership, 1, 10);
        long dailyLoss = 0;
        foreach (var contribution in battlefield.Contributions.Where(c => c.Kind == MilitaryContributionKind.FieldResponse &&
            c.Status is not (ArrivalEstimateStatus.Unreachable or ArrivalEstimateStatus.NotApplicable) && (c.ArrivalDay ?? c.EarliestDay) <= own.Window).Take(4))
        {
            var enemy = queries.Observation.Forces.Find(contribution.Source);
            if (enemy?.FormationId is not int enemyFormation || rules.Formation(enemyFormation, enemy.NavalFormationId, enemy.Area.Position).Combat is not { } attack) continue;
            var enemyStrength = new CombatStrength(enemy.Soldiers, enemy.Morale, 70, 1, 10);
            bool intercepted = own.Participants.Where(p => p.Role == MilitaryCommitmentRole.Field && p.Days <= own.Window).Any(guard =>
            {
                var unit = guard.Unit;
                if (unit.FormationId is not int id || rules.Formation(id, unit.NavalFormationId, enemy.Area.Position).Combat is not { } profile) return false;
                var strength = new CombatStrength(unit.Soldiers, unit.Morale, unit.CommanderId is int commander ? engine.GetAbilities(engine.Snapshot)[commander].Leadership : 40, 1, 10);
                long dealt = CombatMath.TroopDamage(strength, enemyStrength, profile, attack, false, 0);
                long received = CombatMath.TroopDamage(enemyStrength, strength, attack, profile, false, 0);
                return dealt > 0 && (enemy.Soldiers + dealt - 1) / dealt <= (received == 0 ? long.MaxValue : (unit.Soldiers + received - 1) / received);
            });
            if (!intercepted) dailyLoss += CombatMath.TroopDamage(enemyStrength, defender, attack, defense, false, 0);
        }
        if (dailyLoss == 0 || target.Soldiers == 0) return false;
        long survival = (primary.Soldiers + dailyLoss - 1) / dailyLoss;
        return survival < captureDays;
    }

    private void Withdraw(MilitaryCampaign campaign, MilitaryTravelQueries queries)
    {
        foreach (int unitId in campaign.UnitIds.ToArray())
        {
            var unit = engine.Snapshot.Units.FirstOrDefault(u => u.Id == unitId);
            if (unit is null || unit.IsEmbedded || !Controls(unit) || !unit.CanReceiveOrders) continue;
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

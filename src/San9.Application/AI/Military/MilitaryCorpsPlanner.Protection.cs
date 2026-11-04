using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

internal sealed partial class MilitaryCorpsPlanner
{
    // Ordinary damage gives a bounded planning estimate, not a claim about hidden tactics or future orders.
    private int SurvivalDays(SettlementState place, MilitaryTravelQueries queries, IEnumerable<MilitaryPotentialThreat> threats, int? remainingLeadership = null)
    {
        long wallDamage = 0, troopDamage = 0;
        int earliest = int.MaxValue;
        int leadership;
        if (remainingLeadership is int known) leadership = known;
        else
        {
            var traveling = engine.Snapshot.Simulation.Tasks.Where(t => t.Journey is not null).Select(t => t.OfficerId).ToHashSet();
            leadership = engine.Snapshot.Officers.Where(p => p.OwnerId == corps.OwnerId && p.SettlementId == place.Id && p.IsServing && !traveling.Contains(p.Id))
                .Select(p => engine.GetAbilities(engine.Snapshot)[p.Id].Leadership).DefaultIfEmpty(0).Max();
        }
        var defender = new CombatStrength(place.Soldiers, place.Morale, leadership, 1, 10);
        var defense = rules.Place(place).Combat;
        foreach (var threat in threats)
        {
            var body = queries.Observation.Forces.Find(new(threat.UnitId, false));
            if (body?.FormationId is not int formation) continue;
            var active = rules.Formation(formation, body.NavalFormationId, body.Area.Position);
            if (active.Combat is not { } profile) continue;
            earliest = Math.Min(earliest, threat.EarliestDays);
            if (active.Attack?.DamagesWalls == true)
                wallDamage += CombatMath.DurabilityDamage(body.Soldiers, 70, profile.AgainstWalls, place.Durability);
            if (defense is not null && place.Soldiers > 0)
                troopDamage += CombatMath.TroopDamage(new(body.Soldiers, body.Morale, 70, 1, 10), defender, profile, defense, true, place.Durability);
        }
        if (earliest == int.MaxValue) return 60;
        long wall = wallDamage == 0 ? 60 : (place.Durability + wallDamage - 1) / wallDamage;
        long soldiers = place.Soldiers == 0 ? 0 : troopDamage == 0 ? 60 : (place.Soldiers + troopDamage - 1) / troopDamage;
        return (int)Math.Min(60, earliest + Math.Min(wall, soldiers));
    }

    private bool Rescue(MilitaryTravelQueries queries)
    {
        if (corps.Delegation is DomesticDelegation) return false;
        var world = engine.Snapshot;
        var threatened = world.Settlements.Where(Friendly).Select(s => (Place: s, Threat: queries.PotentialThreats(s.Id, 10)))
            .Where(t => t.Threat.Candidates.Any(p => p.ExplicitTarget || p.EarliestDays <= 2))
            .OrderBy(t => SurvivalDays(t.Place, queries, t.Threat.Candidates)).ThenBy(t => t.Place.Id).Take(4);
        foreach (var target in threatened)
        {
            var campaign = batch.State.Campaigns.FirstOrDefault(c => c.ForceId == corps.OwnerId && c.Purpose == MilitaryCampaignPurpose.Relief &&
                c.ObjectiveId == target.Place.Id && c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled));
            if (campaign?.Stage == MilitaryCampaignStage.Withdrawing) continue;
            int survival = SurvivalDays(target.Place, queries, target.Threat.Candidates);
            var promised = campaign is null ? null : CommittedForces(campaign, queries, survival);
            if (promised?.Uncertain == true) { incomplete = true; continue; }
            long committed = promised?.TimelySoldiers ?? 0;
            long incoming = target.Threat.Candidates.Sum(t => (long)t.Soldiers);
            long demand = Math.Max(0, incoming - target.Place.Soldiers - committed);
            if (demand == 0) continue;
            var enemy = target.Threat.Candidates.OrderByDescending(t => t.ExplicitTarget).ThenBy(t => t.EarliestDays).ThenBy(t => t.UnitId).First();
            // Reuse idle field troops before paying for a second departure. Active commitments are not counted twice.
            foreach (var available in world.Units.Where(u => Controls(u) && !u.IsEmbedded && !u.IsPersonnel && u.Soldiers > 0 && u.CanReceiveOrders && !u.CommandedThisXun &&
                (!batch.State.Campaigns.Any(c => c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled) && c.UnitIds.Contains(u.Id)) ||
                    survival <= 10 && batch.State.Campaigns.Any(c => c.Purpose == MilitaryCampaignPurpose.Conquest && c.Stage == MilitaryCampaignStage.Marching &&
                        c.Support.Any(s => s.UnitId == u.Id && s.Role == MilitaryCommitmentRole.Field))))
                .OrderBy(u => rules.Travel(u).AreaAt(u.Position).DistanceTo(rules.SettlementArea(target.Place))).ThenBy(u => u.Id).Take(2))
            {
                var travel = queries.FromUnit(available.Id, new(enemy.UnitId, false), policies: available.Policies, verifyApproach: true, newOrder: true);
                if (travel.Status != ArrivalEstimateStatus.Known || travel.Days > survival && available.Soldiers < incoming) continue;
                var identity = Identity();
                if (!Submit(new OrderUnitCommand(identity.Id, corps.OwnerId, identity.Sequence, available.Id,
                    new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = enemy.UnitId }) { ConfirmAttack = true })) continue;
                foreach (var parent in batch.State.Campaigns.Where(c => c.Purpose == MilitaryCampaignPurpose.Conquest && c.Support.Any(s => s.UnitId == available.Id)).ToArray())
                {
                    var withdrawn = parent.Support.Single(s => s.UnitId == available.Id);
                    Replace(parent, parent with { Support = parent.Support.Remove(withdrawn),
                        InitialSoldiers = parent.InitialSoldiers - Math.Min(parent.InitialSoldiers, withdrawn.InitialSoldiers) });
                }
                if (campaign is null)
                {
                    campaign = new(batch.State.NextId, corps.OwnerId, corps.Id, available.ServicePlaceId, target.Place.Id, target.Place.Id, available.Id,
                        MilitaryCampaignStage.Marching, world.Clock, world.Clock, available.Position, target.Place.Durability)
                    { Purpose = MilitaryCampaignPurpose.Relief, PlannedArrivalDay = world.Clock.DayNumber + travel.Days!.Value,
                        ExpectedOpposition = incoming, InitialSoldiers = available.Soldiers };
                    batch.State = batch.State with { NextId = batch.State.NextId + 1, Campaigns = batch.State.Campaigns.Add(campaign) };
                }
                else AddSupport(campaign, available.Id, available.ServicePlaceId ?? target.Place.Id, MilitaryCommitmentRole.Field, target.Place.Id);
                return true;
            }
            var options = new List<(GameCommand Command, int Source, int Days, long Score, MilitaryCommitmentRole Role)>();
            foreach (var source in Sources(queries, campaign?.Id).Where(s => s.Id != target.Place.Id).Take(3))
            {
                uint spare = Spare(source, queries, campaign?.Id);
                if (spare == 0) continue;
                var field = Organize(source, new(enemy.UnitId, false), (uint)Math.Min(spare, Math.Max(1000, demand)), MilitaryGroupRole.Field, queries, campaign?.Id);
                if (field is not null)
                {
                    int earliest = NavigationArrivalForecast.EarliestDays(rules.Travel(field.FormationId, field.NavalFormationId, field.Soldiers),
                        rules.SettlementArea(source).Position, queries.Observation.Forces.Find(new(enemy.UnitId, false))!.Area);
                    // The field force must be useful on its own when the town falls before contact.
                    if (earliest <= survival || field.Soldiers >= incoming)
                        options.Add((field, source.Id, earliest, 20000L - earliest * 300 - Math.Max(0, incoming - field.Soldiers), MilitaryCommitmentRole.Field));
                }
                if (rules.TransportFormations is not { } pair || MilitaryResourceLedger.ReceivingSpace(world, target.Place) == 0) continue;
                var courier = world.Officers.Where(p => p.OwnerId == corps.OwnerId && p.SettlementId == source.Id && p.IsServing && !p.IsDormant && !p.Acted && p.UnitId is null &&
                    !world.Simulation.Tasks.Any(t => t.OfficerId == p.Id) && !MilitaryResourceLedger.OfficerReserved(batch.State, p.Id, campaign?.Id)).OrderBy(p => p.Id).FirstOrDefault();
                if (courier is null) continue;
                uint load = (uint)Math.Min(Math.Min(spare, Math.Max(1000, demand)), Math.Min(rules.Formations[pair.LandFormationId].SoldierCapacity, MilitaryResourceLedger.ReceivingSpace(world, target.Place)));
                load = DepartureLimit(source, load, [courier.Id], queries);
                int lowerBound = NavigationArrivalForecast.EarliestDays(rules.Travel(pair.LandFormationId, pair.NavalFormationId, load),
                    rules.SettlementArea(source).Position, rules.SettlementArea(target.Place));
                if (load == 0 || lowerBound >= survival) continue;
                var reinforced = target.Place with { Soldiers = target.Place.Soldiers + load };
                int extended = SurvivalDays(reinforced, queries, target.Threat.Candidates);
                if (extended <= survival) continue;
                var id = Identity();
                options.Add((new TransportUnitCommand(id.Id, corps.OwnerId, id.Sequence, source.Id, [courier.Id], load, target.Place.Id), source.Id,
                    lowerBound, 20000L + (extended - survival) * 500L - lowerBound * 300 - load / 10, MilitaryCommitmentRole.Transport));
            }
            foreach (var option in options.OrderByDescending(o => o.Score).ThenBy(o => o.Source))
            {
                if (option.Score <= 0) continue;
                var arrival = option.Command switch
                {
                    DeployUnitCommand d => queries.FromSettlement(d.SettlementId, new(enemy.UnitId, false), d.FormationId, d.NavalFormationId, d.Soldiers,
                        policies: d.Policies, verifyApproach: true),
                    TransportUnitCommand t => queries.FromSettlement(t.SettlementId, new(target.Place.Id, true), rules.TransportFormations!.LandFormationId,
                        rules.TransportFormations.NavalFormationId, t.Soldiers, arrival: NavigationArrival.Region, verifyApproach: true),
                    _ => null
                };
                if (arrival?.Status != ArrivalEstimateStatus.Known || option.Command is TransportUnitCommand && arrival.Days >= survival ||
                    option.Command is DeployUnitCommand planned && arrival.Days > survival && planned.Soldiers < incoming) continue;
                if (ExecuteDeparture(option.Command, campaign?.Id) is not int unitId) continue;
                if (campaign is null)
                {
                    var unit = engine.Snapshot.Units.Single(u => u.Id == unitId);
                    campaign = new(batch.State.NextId, corps.OwnerId, corps.Id, option.Source, target.Place.Id, target.Place.Id, unitId,
                        MilitaryCampaignStage.Marching, world.Clock, world.Clock, unit.Position, target.Place.Durability)
                    { Purpose = MilitaryCampaignPurpose.Relief, PlannedArrivalDay = world.Clock.DayNumber + arrival.Days!.Value, ExpectedOpposition = incoming, InitialSoldiers = unit.Soldiers };
                    batch.State = batch.State with { NextId = batch.State.NextId + 1, Campaigns = batch.State.Campaigns.Add(campaign) };
                }
                else AddSupport(campaign, unitId, option.Source, option.Role, target.Place.Id);
                return true;
            }
        }
        return false;
    }

    private void Evacuate(MilitaryTravelQueries queries)
    {
        var world = engine.Snapshot;
        foreach (var source in world.Settlements.Where(s => Friendly(s) && (s.CorpsId == corps.Id || s.CorpsId is null && corps.IsRoot)).OrderBy(s => s.Id))
        {
            var threats = queries.PotentialThreats(source.Id, 10).Candidates;
            bool danger = threats.Any(t => t.ExplicitTarget || t.EarliestDays <= 2) &&
                (threats.Sum(t => (long)t.Soldiers) > source.Soldiers || SurvivalDays(source, queries, threats) < 10);
            if (!danger)
            {
                batch.State = batch.State with { EvacuatedPlaces = batch.State.EvacuatedPlaces.Remove(source.Id) };
                continue;
            }
            batch.State = batch.State with { EvacuatedPlaces = batch.State.EvacuatedPlaces.SetItem(source.Id, corps.OwnerId) };
            var people = world.Officers.Where(p => p.SettlementId == source.Id && p.OwnerId == corps.OwnerId && p.IsServing && !p.IsDormant && !p.Acted && p.UnitId is null &&
                !world.Simulation.Tasks.Any(t => t.OfficerId == p.Id)).OrderBy(p => p.Id).Take(4).Select(p => p.Id).ToImmutableArray();
            if (people.IsEmpty) continue;
            int survival = SurvivalDays(source, queries, threats);
            foreach (var destination in world.Settlements.Where(s => s.Id != source.Id && Friendly(s) &&
                    queries.PotentialThreats(s.Id, 10).Candidates.Sum(t => (long)t.Soldiers) <= s.Soldiers)
                .OrderBy(s => rules.SettlementArea(s).DistanceTo(rules.SettlementArea(source))).ThenBy(s => s.Id).Take(4))
            {
                var id = Identity();
                var command = new MoveOfficersCommand(id.Id, corps.OwnerId, id.Sequence, people, destination.Id);
                var preview = engine.Preview(context, command);
                if (preview.Rejection != CommandRejection.None || preview.JourneyDays is not long days || days >= survival) continue;
                if (!Submit(command)) continue;
                var campaign = new MilitaryCampaign(batch.State.NextId, corps.OwnerId, corps.Id, source.Id, source.Id, destination.Id, null,
                    MilitaryCampaignStage.Assembling, world.Clock, world.Clock, rules.SettlementArea(source).Position, source.Durability)
                { Purpose = MilitaryCampaignPurpose.Evacuation, DeliveryOfficerId = people[0], ReservedOfficers = people,
                    PlannedArrivalDay = world.Clock.DayNumber + days };
                batch.State = batch.State with { NextId = batch.State.NextId + 1, Campaigns = batch.State.Campaigns.Add(campaign) };
                break;
            }
        }
    }
    private readonly List<(long Revision, int Source, ImmutableHashSet<int> Departing, uint Maximum, bool OfficersCanLeave)> departureLimits = [];

    private uint DepartureLimit(SettlementState source, uint soldiers, IEnumerable<int> departingOfficers, MilitaryTravelQueries queries) =>
        Math.Min(soldiers, DepartureAllowance(source, departingOfficers, queries).Maximum);

    private (uint Maximum, bool OfficersCanLeave) DepartureAllowance(SettlementState source, IEnumerable<int> departingOfficers, MilitaryTravelQueries queries)
    {
        var world = engine.Snapshot;
        var departing = departingOfficers.ToImmutableHashSet();
        var cached = departureLimits.FirstOrDefault(p => p.Revision == world.Revision && p.Source == source.Id && p.Departing.SetEquals(departing));
        if (cached.Departing is not null) return (cached.Maximum, cached.OfficersCanLeave);
        uint maximum = source.Soldiers;
        bool officersCanLeave = true;
        var residents = world.Officers.Where(p => p.OwnerId == corps.OwnerId && p.SettlementId == source.Id && p.IsServing && !p.IsDormant && !departing.Contains(p.Id)).ToArray();
        var threats = queries.PotentialThreats(source.Id, 10).Candidates;
        if ((residents.Length > 0 || source.Wounded > 0) && threats.Any(t => t.ExplicitTarget || t.EarliestDays <= 2))
        {
            var traveling = world.Simulation.Tasks.Where(t => t.Journey is not null).Select(t => t.OfficerId).ToHashSet();
            int remainingLeadership = residents.Where(p => !traveling.Contains(p.Id)).Select(p => engine.GetAbilities(world)[p.Id].Leadership).DefaultIfEmpty(0).Max();
            // Preserve the staff's next order opportunity. If it is already in danger, do not take its garrison away for a distant campaign.
            if (SurvivalDays(source, queries, threats, remainingLeadership) < 10) { maximum = 0; officersCanLeave = false; }
            else
            {
                uint low = 0, high = source.Soldiers;
                while (low < high)
                {
                    uint sent = low + (uint)(((ulong)high - low + 1) / 2);
                    var remaining = source with { Soldiers = source.Soldiers - sent };
                    if (SurvivalDays(remaining, queries, threats, remainingLeadership) >= 10) low = sent;
                    else high = sent - 1;
                }
                maximum = low;
            }
        }
        departureLimits.Add((world.Revision, source.Id, departing, maximum, officersCanLeave));
        return (maximum, officersCanLeave);
    }

    private void RecoverStaff(MilitaryTravelQueries queries)
    {
        var world = engine.Snapshot;
        var busy = world.Simulation.Tasks.Select(t => t.OfficerId).ToHashSet();
        var residents = world.Officers.Where(p => p.OwnerId == corps.OwnerId && p.IsServing && !p.IsDormant && p.UnitId is null && p.SettlementId is not null)
            .ToLookup(p => p.SettlementId!.Value);
        var destinations = world.Settlements.Where(s => OwnSource(s) && s.Soldiers > 0 && !residents.Contains(s.Id) &&
            !batch.State.EvacuatedPlaces.ContainsKey(s.Id) && queries.PotentialThreats(s.Id, 10).Candidates.Sum(t => (long)t.Soldiers) <= s.Soldiers)
            .OrderByDescending(s => s.Soldiers).ThenBy(s => s.Id).Take(2).ToArray();
        foreach (var destination in destinations)
        foreach (var source in world.Settlements.Where(OwnSource).Where(s => s.Id != destination.Id && residents[s.Id].Count() > 1 &&
            queries.PotentialThreats(s.Id, 10).Candidates.IsEmpty).OrderBy(s => s.Id))
        {
            var person = residents[source.Id].Where(p => !p.Acted && !busy.Contains(p.Id) && !MilitaryResourceLedger.OfficerReserved(batch.State, p.Id))
                .OrderByDescending(p => engine.GetAbilities(world)[p.Id].Leadership).ThenBy(p => p.Id).FirstOrDefault();
            if (person is null) continue;
            var id = Identity();
            var command = new MoveOfficersCommand(id.Id, corps.OwnerId, id.Sequence, [person.Id], destination.Id);
            var preview = engine.Preview(context, command);
            if (preview.Rejection != CommandRejection.None || preview.JourneyDays is not long days || !Submit(command)) continue;
            var campaign = new MilitaryCampaign(batch.State.NextId, corps.OwnerId, corps.Id, source.Id, destination.Id, destination.Id, null,
                MilitaryCampaignStage.Assembling, world.Clock, world.Clock, rules.SettlementArea(source).Position, destination.Durability)
            { Purpose = MilitaryCampaignPurpose.Recovery, DeliveryOfficerId = person.Id, ReservedOfficers = [person.Id], PlannedArrivalDay = world.Clock.DayNumber + days };
            batch.State = batch.State with { NextId = batch.State.NextId + 1, Campaigns = batch.State.Campaigns.Add(campaign) };
            return;
        }
    }

}

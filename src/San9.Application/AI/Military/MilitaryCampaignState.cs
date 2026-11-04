using System.Collections.Immutable;
using System.Text.Json.Serialization;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

public enum MilitaryCampaignStage { Marching, Preparing, Consolidating, Withdrawing, Completed, Cancelled, Assembling }
public enum MilitaryCampaignPurpose { Conquest, Relief, Evacuation, Recovery }
public enum MilitaryCommitmentRole { Siege, Field, Transport, Reserve }
public sealed record MilitaryCommitment([property: JsonRequired] int UnitId, [property: JsonRequired] int SourceId,
    [property: JsonRequired] int CorpsId, [property: JsonRequired] MilitaryCommitmentRole Role,
    [property: JsonRequired] uint InitialSoldiers, [property: JsonRequired] int? DestinationId);
public sealed record MilitaryReservation([property: JsonRequired] int SourceId, [property: JsonRequired] int CorpsId,
    [property: JsonRequired] ImmutableArray<int> OfficerIds, [property: JsonRequired] uint Soldiers,
    [property: JsonRequired] long Money, [property: JsonRequired] long NotBeforeDay,
    [property: JsonRequired] long ExpiresDay, [property: JsonRequired] MilitaryCommitmentRole Role);
public sealed record MilitaryCampaign(
    [property: JsonRequired] long Id, [property: JsonRequired] int ForceId, [property: JsonRequired] int CorpsId,
    [property: JsonRequired] int? SourceId, [property: JsonRequired] int ObjectiveId, [property: JsonRequired] int TargetId,
    [property: JsonRequired] int? UnitId, [property: JsonRequired] MilitaryCampaignStage Stage,
    [property: JsonRequired] GameClock Started, [property: JsonRequired] GameClock ProgressAt,
    [property: JsonRequired] WorldPosition LastPosition, [property: JsonRequired] int TargetDurability)
{
    [JsonRequired] public int? DeliveryOfficerId { get; init; }
    [JsonRequired] public MilitaryCampaignPurpose Purpose { get; init; }
    [JsonRequired] public ImmutableArray<MilitaryReservation> Reservations { get; init; } = [];
    [JsonRequired] public ImmutableArray<MilitaryCommitment> Support { get; init; } = [];
    [JsonRequired] public long PlannedArrivalDay { get; init; }
    [JsonRequired] public long ExpectedOpposition { get; init; }
    [JsonRequired] public uint InitialSoldiers { get; init; }
    [JsonRequired] public ImmutableArray<int> ReservedOfficers { get; init; } = [];
    [JsonIgnore] public IEnumerable<int> UnitIds => (UnitId is int lead ? new[] { lead } : []).Concat(Support.Select(s => s.UnitId));
}

/// <summary>Submitted commitments and bounded future departures persist; current resources remain authoritative in Core.</summary>
public sealed record MilitaryCampaignState
{
    [JsonRequired] public ImmutableDictionary<int, int> LastCorps { get; init; } = ImmutableDictionary<int, int>.Empty;
    [JsonRequired] public ImmutableDictionary<int, int> LastSources { get; init; } = ImmutableDictionary<int, int>.Empty;
    [JsonRequired] public ImmutableDictionary<int, int> EvacuatedPlaces { get; init; } = ImmutableDictionary<int, int>.Empty;
    [JsonRequired] public long NextId { get; init; }
    [JsonRequired] public ImmutableArray<MilitaryCampaign> Campaigns { get; init; } = [];

    internal MilitaryCampaignState Reconcile(WorldSnapshot world, MilitaryRulebook? rules = null)
    {
        var units = world.Units.ToDictionary(u => u.Id);
        var places = world.Settlements.ToDictionary(s => s.Id);
        var corps = world.Corps.ToDictionary(c => c.Id);
        var updated = Campaigns.Select(original =>
        {
            var c = original with { Reservations = original.Reservations.Where(r => r.ExpiresDay >= world.Clock.DayNumber &&
                places.GetValueOrDefault(r.SourceId)?.OwnerId == original.ForceId &&
                corps.GetValueOrDefault(r.CorpsId)?.OwnerId == original.ForceId &&
                (places.GetValueOrDefault(r.SourceId)?.CorpsId == r.CorpsId || places.GetValueOrDefault(r.SourceId)?.CorpsId is null && corps.GetValueOrDefault(r.CorpsId)?.IsRoot == true) &&
                places.GetValueOrDefault(r.SourceId)?.Soldiers >= r.Soldiers && corps.GetValueOrDefault(r.CorpsId)?.Treasury.Money >= r.Money &&
                r.OfficerIds.All(id => world.Officers.Any(p => p.Id == id && p.OwnerId == original.ForceId && p.SettlementId == r.SourceId && p.IsServing && !p.IsDormant && !p.Acted && p.UnitId is null) &&
                    !world.Simulation.Tasks.Any(t => t.OfficerId == id)))
                .ToImmutableArray() };
            bool done = c.Stage is MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled;
            if (done) return c with { ReservedOfficers = [], Reservations = [] };
            var physical = c.UnitId is int body ? units.GetValueOrDefault(body) : null;
            var living = c.UnitIds.Select(id => units.GetValueOrDefault(id)).OfType<UnitState>().Where(u => u.OwnerId == c.ForceId).ToArray();
            var organizer = world.Officers.FirstOrDefault(p => p.Id == c.DeliveryOfficerId);
            int? organizerCorps = organizer?.SettlementId is int organizerHome ? places.GetValueOrDefault(organizerHome)?.CorpsId : null;
            int? actualCorps = physical?.OwnerId == c.ForceId ? physical.CorpsId :
                c.Stage == MilitaryCampaignStage.Assembling && organizerCorps is not null ? organizerCorps :
                places.GetValueOrDefault(c.TargetId) is { } assignedPlace && assignedPlace.OwnerId == c.ForceId ? assignedPlace.CorpsId : null;
            if (actualCorps is int assigned && corps.TryGetValue(assigned, out var actual) && actual.OwnerId == c.ForceId)
                c = c with { CorpsId = assigned };
            if (!corps.TryGetValue(c.CorpsId, out var controller) || controller.OwnerId != c.ForceId)
            {
                var root = world.Corps.FirstOrDefault(p => p.OwnerId == c.ForceId && p.IsRoot);
                if (root is null) return c with { Stage = MilitaryCampaignStage.Cancelled };
                c = c with { CorpsId = root.Id };
            }
            if (c.Purpose is MilitaryCampaignPurpose.Evacuation or MilitaryCampaignPurpose.Recovery)
            {
                var officer = world.Officers.FirstOrDefault(p => p.Id == c.DeliveryOfficerId);
                if (officer?.OwnerId != c.ForceId || !officer.IsServing) return c with { Stage = MilitaryCampaignStage.Cancelled, ProgressAt = world.Clock };
                if (officer.SettlementId == c.TargetId && !world.Simulation.Tasks.Any(t => t.OfficerId == officer.Id))
                    return c with { Stage = MilitaryCampaignStage.Completed, ProgressAt = world.Clock, ReservedOfficers = [] };
                return world.Simulation.Tasks.Any(t => t.OfficerId == officer.Id) ? c : c with { Stage = MilitaryCampaignStage.Cancelled, ProgressAt = world.Clock };
            }
            if (c.Purpose == MilitaryCampaignPurpose.Relief)
            {
                if (places.GetValueOrDefault(c.ObjectiveId)?.OwnerId != c.ForceId)
                    return c with { Stage = living.Length == 0 ? MilitaryCampaignStage.Cancelled : MilitaryCampaignStage.Withdrawing };
                if (c.Stage == MilitaryCampaignStage.Withdrawing)
                    return living.Length == 0 ? c with { Stage = MilitaryCampaignStage.Cancelled, ProgressAt = world.Clock } : c;
                bool pressure = rules is not null && world.Units.Any(u => !u.IsEmbedded && !u.IsPersonnel && u.FormationId is not null &&
                    MilitaryDiplomacy.AutomaticEnemy(world, c.ForceId, u.OwnerId) &&
                    NavigationArrivalForecast.EarliestDays(rules.Travel(u), u.Position, rules.SettlementArea(places[c.ObjectiveId]), maximumRange: 10) <= 10);
                if (!pressure)
                    return c.Stage == MilitaryCampaignStage.Consolidating && world.Clock.DayNumber - c.ProgressAt.DayNumber >= 10
                        ? c with { Stage = MilitaryCampaignStage.Completed } : c with { Stage = MilitaryCampaignStage.Consolidating,
                            ProgressAt = c.Stage == MilitaryCampaignStage.Consolidating ? c.ProgressAt : world.Clock };
                return c with { Stage = MilitaryCampaignStage.Marching };
            }
            if (c.Stage == MilitaryCampaignStage.Assembling &&
                (!places.TryGetValue(c.ObjectiveId, out var final) || final.IsDestroyed || final.OwnerId == c.ForceId ||
                    !MilitaryDiplomacy.AutomaticEnemy(world, c.ForceId, final.OwnerId)))
                return c with { Stage = final?.OwnerId == c.ForceId ? living.Length == 0 ? MilitaryCampaignStage.Completed : MilitaryCampaignStage.Consolidating :
                    living.Length == 0 ? MilitaryCampaignStage.Cancelled : MilitaryCampaignStage.Withdrawing, ProgressAt = world.Clock };
            if (physical is null && living.Length > 0)
            {
                physical = living.OrderBy(u => u.Order?.Kind == MilitaryOrderKind.AttackSettlement ? 0 : 1).ThenBy(u => u.Id).First();
                c = c with { UnitId = physical.Id, Support = c.Support.Where(p => p.UnitId != physical.Id).ToImmutableArray(),
                    Stage = c.Stage == MilitaryCampaignStage.Withdrawing ? c.Stage : MilitaryCampaignStage.Preparing };
            }
            if (c.Stage == MilitaryCampaignStage.Assembling)
            {
                var officer = world.Officers.FirstOrDefault(p => p.Id == c.DeliveryOfficerId);
                var staging = places.GetValueOrDefault(c.TargetId);
                if (officer?.OwnerId != c.ForceId || !officer.IsServing || staging?.OwnerId != c.ForceId)
                    return c with { Stage = physical is null ? MilitaryCampaignStage.Cancelled : MilitaryCampaignStage.Withdrawing };
                if (physical is null && officer.SettlementId == staging.Id && staging.Soldiers > 0 &&
                    !world.Simulation.Tasks.Any(t => t.OfficerId == officer.Id))
                {
                    if (officer.Acted) return c;
                    c = c with { Stage = MilitaryCampaignStage.Preparing, UnitId = null, ProgressAt = world.Clock };
                }
                else if (physical is null && !world.Simulation.Tasks.Any(t => t.OfficerId == officer.Id))
                    return c with { Stage = MilitaryCampaignStage.Cancelled };
                else return physical is not null && physical.Position != c.LastPosition
                    ? c with { LastPosition = physical.Position, ProgressAt = world.Clock } : c;
            }
            if (!places.TryGetValue(c.ObjectiveId, out var objective) || objective.IsDestroyed)
                return c with { Stage = physical is null ? MilitaryCampaignStage.Cancelled : MilitaryCampaignStage.Withdrawing };
            bool conquered = objective.OwnerId == c.ForceId;
            var unit = c.UnitId is int moving ? units.GetValueOrDefault(moving) : null;
            if (unit is null || unit.OwnerId != c.ForceId)
                return c with { Stage = conquered ? MilitaryCampaignStage.Completed :
                    places.GetValueOrDefault(c.TargetId)?.OwnerId == c.ForceId ? MilitaryCampaignStage.Preparing : MilitaryCampaignStage.Cancelled };
            if (c.Stage == MilitaryCampaignStage.Withdrawing) return c;
            if (conquered)
                return c.Stage == MilitaryCampaignStage.Consolidating
                    ? world.Clock.DayNumber - c.ProgressAt.DayNumber >= 10 ? c with { Stage = MilitaryCampaignStage.Completed } : c
                    : c with { Stage = MilitaryCampaignStage.Consolidating, ProgressAt = world.Clock };
            if (places.GetValueOrDefault(c.TargetId)?.OwnerId == c.ForceId)
                return c with { Stage = MilitaryCampaignStage.Preparing };
            if (unit.Order is not { Kind: MilitaryOrderKind.AttackSettlement } order || order.SettlementId != c.TargetId)
                return c with { Stage = MilitaryCampaignStage.Withdrawing };
            var target = places.GetValueOrDefault(c.TargetId);
            bool progress = unit.Position != c.LastPosition || target is not null && target.Durability < c.TargetDurability;
            var next = c with { LastPosition = unit.Position, TargetDurability = target?.Durability ?? 0,
                ProgressAt = progress ? world.Clock : c.ProgressAt };
            if (target?.OwnerId == c.ForceId) return next with { Stage = MilitaryCampaignStage.Preparing };
            if (target is null || target.IsDestroyed || !MilitaryDiplomacy.AutomaticEnemy(world, c.ForceId, target.OwnerId) ||
                world.Clock.DayNumber - next.ProgressAt.DayNumber >= 60)
                return next with { Stage = MilitaryCampaignStage.Withdrawing };
            return next;
        }).Select(c => c.Stage is MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled or MilitaryCampaignStage.Withdrawing
            ? c with { ReservedOfficers = [], Reservations = [] } : c).ToImmutableArray();
        // Completed history is explanatory, not an ever-growing command log.
        var retained = updated.Where(c => c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled))
            .Concat(updated.Where(c => c.Stage is MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled).TakeLast(32))
            .OrderBy(c => c.Id).ToImmutableArray();
        var evacuation = EvacuatedPlaces.Where(p => places.GetValueOrDefault(p.Key)?.OwnerId == p.Value).ToImmutableDictionary();
        return this with { Campaigns = retained, EvacuatedPlaces = evacuation };
    }

    internal void Validate(WorldSnapshot world)
    {
        if (NextId < 0 || Campaigns.IsDefault || LastCorps is null || LastSources is null || EvacuatedPlaces is null ||
            EvacuatedPlaces.Any(p => p.Key < 0 || p.Value < 0) ||
            LastCorps.Any(p => p.Key < 0 || p.Value < 0) || LastSources.Any(p => p.Key < 0 || p.Value < 0) || Campaigns.Any(c => c is null || c.Id < 0 || c.Id >= NextId ||
                c.ForceId < 0 || c.CorpsId < 0 || c.SourceId < 0 || c.ObjectiveId < 0 || c.TargetId < 0 || c.UnitId < 0 ||
                !Enum.IsDefined(c.Stage) || c.Started.DayNumber < world.Simulation.Started.DayNumber ||
                c.ProgressAt.DayNumber < c.Started.DayNumber || c.ProgressAt.DayNumber > world.Clock.DayNumber || c.TargetDurability < 0 || c.DeliveryOfficerId is < 0 || c.LastPosition.X < 0 || c.LastPosition.Y < 0 ||
                !world.Forces.Any(f => f.Id == c.ForceId) || c.SourceId is int source && !world.Settlements.Any(p => p.Id == source) ||
                !world.Settlements.Any(p => p.Id == c.ObjectiveId) || !world.Settlements.Any(p => p.Id == c.TargetId) ||
                !Enum.IsDefined(c.Purpose) || c.PlannedArrivalDay < 0 || c.ExpectedOpposition < 0 || c.Support.IsDefault || c.Reservations.IsDefault || c.Reservations.Any(r => r is null || r.SourceId < 0 || r.CorpsId < 0 ||
                    r.OfficerIds.IsDefaultOrEmpty || r.OfficerIds.Any(id => !world.Officers.Any(p => p.Id == id)) || r.OfficerIds.Distinct().Count() != r.OfficerIds.Length ||
                    !world.Settlements.Any(p => p.Id == r.SourceId) || r.Money < 0 || r.NotBeforeDay < 0 || r.ExpiresDay < r.NotBeforeDay || !Enum.IsDefined(r.Role)) ||
                c.ReservedOfficers.IsDefault || c.ReservedOfficers.Any(id => !world.Officers.Any(p => p.Id == id)) || c.ReservedOfficers.Distinct().Count() != c.ReservedOfficers.Length ||
                c.Support.Any(p => p is null || p.UnitId < 0 || p.SourceId < 0 || p.CorpsId < 0 || !Enum.IsDefined(p.Role) || p.DestinationId is int destination && !world.Settlements.Any(s => s.Id == destination) || !world.Settlements.Any(s => s.Id == p.SourceId)) ||
                c.UnitIds.Distinct().Count() != c.UnitIds.Count() ||
                c.Stage == MilitaryCampaignStage.Assembling && (c.DeliveryOfficerId is not int courier || !world.Officers.Any(p => p.Id == courier))) ||
            Campaigns.Select(c => c.Id).Distinct().Count() != Campaigns.Length ||
            Campaigns.Where(c => c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled))
                .SelectMany(c => c.UnitIds).Distinct().Count() != Campaigns.Where(c => c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled)).Sum(c => c.UnitIds.Count()))
            throw new ArgumentException("Invalid military campaign state.");
    }
}

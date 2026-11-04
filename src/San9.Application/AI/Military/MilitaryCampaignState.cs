using System.Collections.Immutable;
using System.Text.Json.Serialization;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

public enum MilitaryCampaignStage { Marching, Preparing, Consolidating, Withdrawing, Completed, Cancelled, Assembling }
public sealed record MilitaryCampaign(
    [property: JsonRequired] long Id, [property: JsonRequired] int ForceId, [property: JsonRequired] int CorpsId,
    [property: JsonRequired] int SourceId, [property: JsonRequired] int ObjectiveId, [property: JsonRequired] int TargetId,
    [property: JsonRequired] int? UnitId, [property: JsonRequired] MilitaryCampaignStage Stage,
    [property: JsonRequired] GameClock Started, [property: JsonRequired] GameClock ProgressAt,
    [property: JsonRequired] WorldPosition LastPosition, [property: JsonRequired] int TargetDurability)
{
    [JsonRequired] public int? DeliveryOfficerId { get; init; }
}

/// <summary>Only submitted commitments persist. Draft choices and reservations live in public deployment plans.</summary>
public sealed record MilitaryCampaignState
{
    [JsonRequired] public long NextId { get; init; }
    [JsonRequired] public ImmutableArray<MilitaryCampaign> Campaigns { get; init; } = [];

    internal MilitaryCampaignState Reconcile(WorldSnapshot world)
    {
        var units = world.Units.ToDictionary(u => u.Id);
        var places = world.Settlements.ToDictionary(s => s.Id);
        var corps = world.Corps.ToDictionary(c => c.Id);
        var updated = Campaigns.Select(original =>
        {
            var c = original;
            if (c.Stage is MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled) return c;
            var physical = c.UnitId is int body ? units.GetValueOrDefault(body) : null;
            if (c.Stage == MilitaryCampaignStage.Assembling)
            {
                var officer = world.Officers.FirstOrDefault(p => p.Id == c.DeliveryOfficerId);
                var staging = places.GetValueOrDefault(c.TargetId);
                if (officer?.OwnerId != c.ForceId || !officer.IsServing || staging?.OwnerId != c.ForceId)
                    return c with { Stage = physical is null ? MilitaryCampaignStage.Cancelled : MilitaryCampaignStage.Withdrawing };
                if (officer.SettlementId == staging.Id && staging.Soldiers > 0)
                {
                    if (officer.Acted) return c;
                    c = c with { Stage = MilitaryCampaignStage.Preparing, UnitId = null, ProgressAt = world.Clock };
                }
                else if (physical is null && !world.Simulation.Tasks.Any(t => t.OfficerId == officer.Id))
                    return c with { Stage = MilitaryCampaignStage.Cancelled };
                else return physical is not null && physical.Position != c.LastPosition
                    ? c with { LastPosition = physical.Position, ProgressAt = world.Clock } : c;
            }
            int? actualCorps = physical?.OwnerId == c.ForceId ? physical.CorpsId :
                places.GetValueOrDefault(c.TargetId) is { } assignedPlace && assignedPlace.OwnerId == c.ForceId ? assignedPlace.CorpsId : null;
            if (actualCorps is int assigned && corps.TryGetValue(assigned, out var actual) && actual.OwnerId == c.ForceId)
                c = c with { CorpsId = assigned };
            if (!corps.TryGetValue(c.CorpsId, out var controller) || controller.OwnerId != c.ForceId)
            {
                var root = world.Corps.FirstOrDefault(p => p.OwnerId == c.ForceId && p.IsRoot);
                if (root is null) return c with { Stage = MilitaryCampaignStage.Cancelled };
                c = c with { CorpsId = root.Id };
            }
            if (!places.TryGetValue(c.ObjectiveId, out var objective) || objective.IsDestroyed)
                return c with { Stage = physical is null ? MilitaryCampaignStage.Cancelled : MilitaryCampaignStage.Withdrawing };
            bool conquered = objective.OwnerId == c.ForceId;
            var unit = c.UnitId is int moving ? units.GetValueOrDefault(moving) : null;
            if (unit is null || unit.OwnerId != c.ForceId)
                return c with { Stage = conquered ? MilitaryCampaignStage.Completed :
                    places.GetValueOrDefault(c.TargetId)?.OwnerId == c.ForceId ? MilitaryCampaignStage.Preparing : MilitaryCampaignStage.Cancelled };
            if (c.Stage == MilitaryCampaignStage.Withdrawing)
                return unit.Order?.Kind == MilitaryOrderKind.Return ? c : c with { Stage = MilitaryCampaignStage.Withdrawing };
            if (conquered)
                return c.Stage == MilitaryCampaignStage.Consolidating
                    ? world.Clock.DayNumber - c.ProgressAt.DayNumber >= 10 ? c with { Stage = MilitaryCampaignStage.Completed } : c
                    : c with { Stage = MilitaryCampaignStage.Consolidating, ProgressAt = world.Clock };
            if (places.GetValueOrDefault(c.TargetId)?.OwnerId == c.ForceId)
                return c with { Stage = MilitaryCampaignStage.Preparing };
            if (unit.Order is not { Kind: MilitaryOrderKind.AttackSettlement } order || order.SettlementId != c.TargetId)
                return c with { Stage = MilitaryCampaignStage.Cancelled };
            var target = places.GetValueOrDefault(c.TargetId);
            bool progress = unit.Position != c.LastPosition || target is not null && target.Durability < c.TargetDurability;
            var next = c with { LastPosition = unit.Position, TargetDurability = target?.Durability ?? 0,
                ProgressAt = progress ? world.Clock : c.ProgressAt };
            if (target?.OwnerId == c.ForceId) return next with { Stage = MilitaryCampaignStage.Preparing };
            if (target is null || target.IsDestroyed || !MilitaryDiplomacy.AutomaticEnemy(world, c.ForceId, target.OwnerId) ||
                world.Clock.DayNumber - next.ProgressAt.DayNumber >= 60)
                return next with { Stage = MilitaryCampaignStage.Withdrawing };
            return next;
        }).ToImmutableArray();
        // Completed history is explanatory, not an ever-growing command log.
        var retained = updated.Where(c => c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled))
            .Concat(updated.Where(c => c.Stage is MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled).TakeLast(32))
            .OrderBy(c => c.Id).ToImmutableArray();
        return Campaigns.SequenceEqual(retained) ? this : this with { Campaigns = retained };
    }

    internal void Validate(WorldSnapshot world)
    {
        if (NextId < 0 || Campaigns.IsDefault || Campaigns.Any(c => c is null || c.Id < 0 || c.Id >= NextId ||
                c.ForceId < 0 || c.CorpsId < 0 || c.SourceId < 0 || c.ObjectiveId < 0 || c.TargetId < 0 || c.UnitId < 0 ||
                !Enum.IsDefined(c.Stage) || c.Started.DayNumber < world.Simulation.Started.DayNumber ||
                c.ProgressAt.DayNumber < c.Started.DayNumber || c.ProgressAt.DayNumber > world.Clock.DayNumber || c.TargetDurability < 0 || c.DeliveryOfficerId is < 0 || c.LastPosition.X < 0 || c.LastPosition.Y < 0 ||
                !world.Forces.Any(f => f.Id == c.ForceId) || !world.Settlements.Any(p => p.Id == c.SourceId) ||
                !world.Settlements.Any(p => p.Id == c.ObjectiveId) || !world.Settlements.Any(p => p.Id == c.TargetId) ||
                c.Stage == MilitaryCampaignStage.Assembling && (c.DeliveryOfficerId is not int courier || !world.Officers.Any(p => p.Id == courier))) ||
            Campaigns.Select(c => c.Id).Distinct().Count() != Campaigns.Length ||
            Campaigns.Where(c => c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled))
                .Select(c => c.UnitId).OfType<int>().Distinct().Count() != Campaigns.Count(c => c.UnitId is not null && c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled)))
            throw new ArgumentException("Invalid military campaign state.");
    }
}

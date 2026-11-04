using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

public enum MilitaryContributionKind { Garrison, PublishedArrival, FieldResponse, Mobilization }
public sealed record MilitaryContribution(MapObjectReference Source, MilitaryContributionKind Kind, long Soldiers,
    int EarliestDay, int? ArrivalDay, ArrivalEstimateStatus Status);
public sealed record MilitaryBattlefield(int SettlementId, ImmutableArray<MilitaryContribution> Contributions)
{
    public bool Truncated { get; init; }
    public long PublishedAt(int day) => Contributions.Where(c => c.Kind is MilitaryContributionKind.Garrison or MilitaryContributionKind.PublishedArrival &&
        c.ArrivalDay <= day).Sum(c => c.Soldiers);
    public long PossibleAt(int day) => Contributions.Where(c => c.Status is not (ArrivalEstimateStatus.Unreachable or ArrivalEstimateStatus.NotApplicable) &&
        (c.ArrivalDay ?? c.EarliestDay) <= day).Sum(c => c.Soldiers);
    public bool HasUnresolvedArrivals => Truncated || Contributions.Any(c => c.Status is ArrivalEstimateStatus.Incomplete or ArrivalEstimateStatus.Waiting);
}

/// <summary>A single-target response hypothesis. Sources are unique; unknown arrivals never count as already present.
/// Public officer presence bounds possible mobilization, without reading enemy acted flags, accounts or hidden tactics.</summary>
internal static class MilitaryBattlefieldAnalysis
{
    internal static MilitaryBattlefield Assess(MilitaryTravelQueries queries, MilitaryRulebook rules, int settlementId, int days)
    {
        var index = queries.Observation.Forces;
        var target = index.Find(new(settlementId, true)) ?? throw new ArgumentException("Unknown battlefield.");
        var facts = ImmutableArray.CreateBuilder<MilitaryContribution>();
        facts.Add(new(target.Object, MilitaryContributionKind.Garrison, target.Soldiers, 0, 0, ArrivalEstimateStatus.Known));
        if (target.OwnerId is not int owner) return new(settlementId, facts.ToImmutable());
        var publicArrivals = queries.Observation.UnitsTargeting(target.Object).Where(id =>
            queries.Observation.FindUnit(id) is { OwnerId: var force, Order.Value.Kind: MilitaryOrderKind.Return or MilitaryOrderKind.Transport } && force == owner).ToHashSet();
        int maximumRange = rules.Formations.Values.Select(f => f.Attack is { } a ? a.Range + a.MaximumElevationBonus : 1).DefaultIfEmpty(1).Max();
        var mobile = index.Objects.Where(p => p.Kind == MilitaryPresenceKind.Unit && p.OwnerId == owner && p.FormationId is not null &&
                (rules.Purpose(p.FormationId.Value) == UnitPurpose.Combat || publicArrivals.Contains(p.Object.Id)))
            .Select(p => (Body: p, Earliest: NavigationArrivalForecast.EarliestDays(rules.Travel(p.FormationId!.Value,
                p.NavalFormationId, p.Soldiers), p.Area.Position, target.Area, maximumRange: maximumRange)))
            .Where(p => p.Earliest <= days || publicArrivals.Contains(p.Body.Object.Id))
            .OrderBy(p => publicArrivals.Contains(p.Body.Object.Id) ? 0 : 1).ThenBy(p => p.Earliest).ThenBy(p => p.Body.Object.Id).ToArray();
        foreach (var unit in mobile)
        {
            bool returning = publicArrivals.Contains(unit.Body.Object.Id);
            var arrival = queries.Budget.RemainingDetailedRoutes > 1 ? queries.FromUnit(unit.Body.Object.Id, target.Object,
                arrival: returning ? NavigationArrival.Region : NavigationArrival.Range) : null;
            facts.Add(new(unit.Body.Object, returning ? MilitaryContributionKind.PublishedArrival : MilitaryContributionKind.FieldResponse,
                unit.Body.Soldiers, unit.Earliest, arrival?.Status == ArrivalEstimateStatus.Known ? arrival.Days : null, arrival?.Status ?? ArrivalEstimateStatus.Incomplete));
        }
        var formation = rules.Formations.Values.Where(f => f.IsCommon && f.RequiredTacticId is null &&
            f.TravelMode == TravelMode.Land && rules.Purpose(f.Id) == UnitPurpose.Combat && rules.ValidFormationPair(f.Id, rules.BasicNavalFormationId))
            .OrderByDescending(f => f.MovementBudget).ThenBy(f => f.Id).FirstOrDefault();
        bool truncated = false;
        if (formation is not null)
        {
            var travel = rules.Travel(formation.Id, rules.BasicNavalFormationId, formation.SoldierCapacity);
            var sources = index.ForForce(owner).Where(p => p.Kind == MilitaryPresenceKind.Settlement && p.Object != target.Object &&
                    p.StationedOfficers > 0 && p.Soldiers > 0)
                .Select(p => (Body: p, Earliest: NavigationArrivalForecast.EarliestDays(travel, p.Area.Position, target.Area)))
                .Where(p => p.Earliest <= days).OrderBy(p => p.Earliest).ThenBy(p => p.Body.Object.Id).ToArray();
            truncated = sources.Length > 2;
            foreach (var source in sources.Take(2))
            {
                long reserve = queries.PotentialThreats(source.Body.Object.Id, 10, owner).Candidates.Sum(p => (long)p.Soldiers);
                uint troops = (uint)Math.Min(formation.SoldierCapacity, Math.Max(0, source.Body.Soldiers - reserve));
                if (troops == 0) continue;
                var arrival = queries.Budget.RemainingDetailedRoutes > 1 ? queries.FromSettlement(source.Body.Object.Id, target.Object, formation.Id,
                    rules.BasicNavalFormationId, troops) : null;
                facts.Add(new(source.Body.Object, MilitaryContributionKind.Mobilization, troops, source.Earliest,
                    arrival?.Status == ArrivalEstimateStatus.Known ? arrival.Days : null, arrival?.Status ?? ArrivalEstimateStatus.Incomplete));
                if (queries.Budget.Exhausted) break;
            }
        }
        return new(settlementId, facts.ToImmutable()) { Truncated = truncated };
    }
}

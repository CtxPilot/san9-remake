using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

/// <summary>One operation's shared military work. A force shares its allowance across all corps.</summary>
internal sealed class MilitaryTurnBatch(MilitaryIntelligencePublication? publication, MilitaryCampaignState state)
{
    private MilitaryIntelligencePublication? intelligence = publication;
    private MilitaryTransportNetwork? network;
    private readonly Dictionary<int, MilitaryQueryBudget> budgets = [];
    private readonly Dictionary<int, MilitaryQueryBudget> allocationBudgets = [];
    private readonly Dictionary<int, int> groups = [];
    private readonly Dictionary<int, int> groupCeilings = [];
    private readonly Dictionary<int, MilitaryQueryBudget> slices = [];
    private readonly Dictionary<(MilitaryForceIndex Forces, int Target, int Days), MilitaryBattlefield> battlefields = [];
    private readonly Dictionary<(MilitaryForceIndex Forces, UnitState Unit, MapObjectReference Target), MilitaryTravelEstimate> committedArrivals = [];
    internal MilitaryCampaignState State { get; set; } = state;

    internal MilitaryTravelEstimate CommittedArrival(MilitaryTravelQueries queries, UnitState unit, MapObjectReference target)
    {
        var key = (queries.Observation.Forces, unit, target);
        if (committedArrivals.TryGetValue(key, out var prior)) return prior;
        var arrival = queries.FromUnit(unit.Id, target, arrival: unit.Order?.Kind == MilitaryOrderKind.Transport ? NavigationArrival.Region : NavigationArrival.Range);
        if (arrival.Status is ArrivalEstimateStatus.Known or ArrivalEstimateStatus.Unreachable) committedArrivals[key] = arrival;
        return arrival;
    }

    internal void BeginCorps(int force, int remainingCorps, bool finalizing = false)
    {
        if (!budgets.TryGetValue(force, out var total))
        {
            budgets[force] = total = new(maximumDetailedRoutes: 12);
            allocationBudgets[force] = total.Partition(196608, 4, 8);
        }
        slices[force] = (finalizing ? total : allocationBudgets[force]).Slice(remainingCorps);
        int spent = groups.GetValueOrDefault(force);
        groupCeilings[force] = Math.Min(32, spent + Math.Max(1, (32 - spent) / Math.Max(1, remainingCorps)));
    }

    internal MilitaryBattlefield Battlefield(MilitaryTravelQueries queries, MilitaryRulebook rules, int target, int days)
    {
        if (queries.Observation.Forces.Find(new(target, true))?.OwnerId == queries.Observation.ViewerForceId)
            return MilitaryBattlefieldAnalysis.Assess(queries, rules, target, days);
        var key = (queries.Observation.Forces, target, days);
        if (!battlefields.TryGetValue(key, out var value))
            battlefields[key] = value = MilitaryBattlefieldAnalysis.Assess(queries, rules, target, days);
        return value;
    }

    internal int RemainingGroups(int force) => groupCeilings.GetValueOrDefault(force, 32) - groups.GetValueOrDefault(force);
    internal void SpendGroups(int force, int count)
    {
        if (count < 0 || count > RemainingGroups(force)) throw new ArgumentOutOfRangeException(nameof(count));
        groups[force] = groups.GetValueOrDefault(force) + count;
    }

    internal MilitaryTravelQueries Queries(WorldSnapshot world, MilitaryRulebook rules, int force)
    {
        if (intelligence is null || intelligence.PublishedAt != world.Clock)
            intelligence = MilitaryIntelligencePublication.Publish(world, rules);
        if (!budgets.TryGetValue(force, out var budget)) budgets.Add(force, budget = new());
        var own = world.Units.Where(u => u.OwnerId == force).ToDictionary(u => u.Id,
            u => new MilitaryOwnJourney(u.DepartureDaysRemaining, u.MovementRemainder, u.ActiveWaypoint));
        return new(intelligence.Observe(world, rules, force), rules, network ??= new(rules), slices.GetValueOrDefault(force) ?? budget,
            (owner, other) => MilitaryDiplomacy.AutomaticEnemy(world, owner, other), own);
    }
}

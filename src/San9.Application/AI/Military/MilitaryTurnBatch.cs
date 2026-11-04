using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

/// <summary>One operation's shared military work. A force shares its allowance across all corps.</summary>
internal sealed class MilitaryTurnBatch(MilitaryIntelligencePublication? publication, MilitaryCampaignState state)
{
    private MilitaryIntelligencePublication? intelligence = publication;
    private MilitaryTransportNetwork? network;
    private readonly Dictionary<int, MilitaryQueryBudget> budgets = [];
    private readonly Dictionary<int, int> groups = [];
    internal MilitaryCampaignState State { get; set; } = state;

    internal int RemainingGroups(int force) => 32 - groups.GetValueOrDefault(force);
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
        return new(intelligence.Observe(world, rules, force), rules, network ??= new(rules), budget,
            (owner, other) => MilitaryDiplomacy.AutomaticEnemy(world, owner, other), own);
    }
}

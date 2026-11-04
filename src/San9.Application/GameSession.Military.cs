using San9.Application.AI.Military;
using San9.Core.Simulation;

namespace San9.Application;

public sealed partial class GameSession
{
    private MilitaryTransportNetwork? _militaryTransport;

    /// <summary>Player and automatic planners use the same published observation and query rules.
    /// A caller shares this batch across corps/tasks, then creates a new batch after relevant changes.</summary>
    public MilitaryTravelQueries MilitaryQueries(int viewerForceId, MilitaryQueryBudget? budget = null)
    {
        var rules = MilitaryRules ?? throw new InvalidOperationException("This scenario has no military map.");
        var world = Snapshot;
        var own = world.Units.Where(u => u.OwnerId == viewerForceId).ToDictionary(u => u.Id,
            u => new MilitaryOwnJourney(u.DepartureDaysRemaining, u.MovementRemainder, u.ActiveWaypoint));
        return new(ObserveMilitary(viewerForceId), rules, _militaryTransport ??= new(rules), budget ?? new(),
            (source, owner) => MilitaryDiplomacy.AutomaticEnemy(world, source, owner), own);
    }
}

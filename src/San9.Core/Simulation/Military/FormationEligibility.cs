using San9.Core.Domain;

namespace San9.Core.Simulation;

internal static class FormationEligibility
{
    internal static bool CanUse(WorldMutation world, int owner, IEnumerable<int> members,
        FormationRule formation, TreasureRulebook treasures)
    {
        if (formation.MixedTroopsOnly && !world.Simulation.NewGame.MixedTroopsEnabled ||
            world.Simulation.NewGame.MixedTroopsEnabled && formation.ResearchProjectId is string project &&
            !world.Forces[owner].CompletedResearch.Contains(project))
            return false;
        if (formation.RequiredTacticId is not int required) return true;
        return MilitaryTactics.HasTactic(world, owner, members, required, treasures);
    }
}

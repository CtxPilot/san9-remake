using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI;

internal static class OriginalAiStrategyPermissions
{
    internal static bool Allows(WorldSnapshot world, CompanionSettings companion, int forceId, OriginalAiWork work)
    {
        AutomaticStrategyKind? kind = work switch
        {
            OriginalAiWork.FalseReport => AutomaticStrategyKind.FalseReport,
            OriginalAiWork.Confuse => AutomaticStrategyKind.Disorder,
            OriginalAiWork.Encourage => AutomaticStrategyKind.Inspire,
            OriginalAiWork.Reinforce => AutomaticStrategyKind.Reinforce,
            OriginalAiWork.Alienate => AutomaticStrategyKind.Alienate,
            OriginalAiWork.Burn => AutomaticStrategyKind.Burn,
            OriginalAiWork.Seize => AutomaticStrategyKind.Seize,
            OriginalAiWork.Gossip => AutomaticStrategyKind.Gossip,
            _ => null,
        };
        if (kind is null) return true;
        bool player = world.Simulation.Controls.Any(c => c.ForceId == forceId && c.Kind == ControllerKind.Player);
        var permissions = player ? companion.PlayerDelegatedStrategyPermissions : companion.ComputerStrategyPermissions;
        return permissions.Allows(kind.Value);
    }
}

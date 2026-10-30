using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Presentation;

internal static class OfficerArrival
{
    public static CatalogCell Read(StrategySnapshot view, OfficerState person, PersistentTask? task, int? viewerForceId)
    {
        // Corps retain their root force as OwnerId. Visibility precedes applicability.
        if (person.OwnerId != viewerForceId) return new("**", IsUnavailable: true);
        if (person.UnitId is not null || task is null || PersonnelJourneyTiming.InitialDays(task) is not long days)
            return CatalogCell.Unavailable;
        int elapsed = PersonnelJourneyTiming.ElapsedDays(task, view.World.Clock,
            view.World.Simulation.CurrentDateCountdownConsumed);
        return Math.Max(0, days - elapsed);
    }
}

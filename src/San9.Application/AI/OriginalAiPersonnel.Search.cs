using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI;

internal sealed partial class OriginalAiPersonnel
{
    private const int MaximumSearchJourneyDays = 255;

    /// <summary>AI search dispatch over destinations supplied by the personnel work.</summary>
    internal OriginalAiWorkReport Search(int limit, IEnumerable<int> destinationIds)
    {
        int used = 0, submitted = 0, rejected = 0, empty = 0;
        var allowed = engine.GetSearchPlaces(Owner).ToHashSet();
        var targets = destinationIds.Distinct().Where(allowed.Contains).ToList();
        var actors = Actors(45).Select(p => p.Id).Where(actor => targets.Any(target =>
            engine.GetPersonnelJourney(Owner, actor, target, PersonnelJourneyKind.Search, includeAdvice: false)
                .DistanceDays is >= 0 and <= MaximumSearchJourneyDays)).ToList();
        while (used < limit && targets.Count > 0 && actors.Count > 0 && World.Simulation.Boundary == SimulationBoundary.Stable)
        {
            (int Actor, int Target)? pair = null;
            foreach (int target in targets)
            {
                foreach (int actor in actors)
                {
                    var preview = engine.GetPersonnelJourney(Owner, actor, target, PersonnelJourneyKind.Search);
                    if (!preview.Available || preview.DistanceDays is not (>= 0 and <= MaximumSearchJourneyDays) ||
                        preview.Advice.Opinion == AdviserOpinion.Unfavorable) continue;
                    pair = (actor, target);
                    break;
                }
                if (pair is not null) break;
            }
            if (pair is not { } chosen)
            {
                empty += limit - used;
                used = limit;
                break;
            }
            actors.Remove(chosen.Actor);
            targets.Remove(chosen.Target);
            used++;
            if (submit(Command((id, force, sequence) => new SearchCommand(id, force, sequence, [chosen.Actor], chosen.Target)))) submitted++;
            else rejected++;
        }
        return new(corpsId, OriginalAiWork.Search,
            used >= limit ? OriginalAiWorkEnd.AttemptsExhausted : OriginalAiWorkEnd.NoCandidates, used, submitted)
            { Rejected = rejected, EmptyPlans = empty };
    }
}

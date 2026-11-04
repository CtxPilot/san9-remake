using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

internal enum MilitaryPlanningPass { Complete, Allocate, Finalize }

public enum MilitaryDecisionReason
{
    NoMap, NoSource, NoGroup, NoTarget, Unreachable, NotWorthwhile, AwaitingAssembly,
    ExistingCommitment, Consolidating, Withdrawing, Committed, AnalysisIncomplete, PublicRejected, SourceExposed
}

/// <summary>The sole automatic military executor. Every effect uses a public command; no legacy fallback.</summary>
internal static class MilitaryTurnRunner
{
    internal static bool Replaces(OriginalAiWork work) => work is OriginalAiWork.Attack or OriginalAiWork.FulfillRequest or
        OriginalAiWork.Deploy or OriginalAiWork.Retreat or OriginalAiWork.Transport;

    internal static void RunForce(SimulationEngine engine, ScenarioSessionDefinition definition, ControlSeat seat,
        MilitaryTurnBatch batch, List<DomainEvent> events, List<OriginalAiWorkReport> reports)
    {
        int last = batch.State.LastCorps.GetValueOrDefault(seat.ForceId, -1);
        var available = engine.Snapshot.Corps.Where(c => c.OwnerId == seat.ForceId &&
            (seat.Kind == ControllerKind.Automatic || c.ControlMode == CorpsControlMode.Automatic)).OrderBy(c => c.Id).ToArray();
        var ordered = available.Where(c => c.Id > last).Concat(available.Where(c => c.Id <= last)).ToArray();
        var forceReports = new Dictionary<int, int>();
        int? progressed = null;
        int remaining = ordered.Length;
        foreach (var corps in ordered)
        {
            if (engine.Snapshot.Simulation.Boundary != SimulationBoundary.Stable) break;
            var context = new CommandContext(corps.ControlMode == CorpsControlMode.Automatic ? CorpsControl.ControllerId(corps.Id) : seat.ControllerId);
            batch.BeginCorps(seat.ForceId, remaining--);
            var report = Run(engine, definition, corps, context, batch, events, MilitaryPlanningPass.Allocate);
            forceReports[corps.Id] = reports.Count;
            reports.Add(report);
            if (report.GroupEvaluations > 0 || report.Attempts > 0 || report.PathSearches > 0) progressed = corps.Id;
        }
        if (definition.MilitaryRules is not null)
        {
            remaining = ordered.Length;
            foreach (var earlier in ordered)
            {
                if (engine.Snapshot.Simulation.Boundary != SimulationBoundary.Stable) break;
                var current = engine.Snapshot.Corps.FirstOrDefault(c => c.Id == earlier.Id && c.OwnerId == seat.ForceId);
                if (current is null || !forceReports.TryGetValue(current.Id, out int index)) continue;
                batch.BeginCorps(seat.ForceId, remaining--, finalizing: true);
                var context = new CommandContext(current.ControlMode == CorpsControlMode.Automatic ? CorpsControl.ControllerId(current.Id) : seat.ControllerId);
                var after = Run(engine, definition, current, context, batch, events, MilitaryPlanningPass.Finalize);
                var before = reports[index];
                reports[index] = before with { Attempts = before.Attempts + after.Attempts, Submitted = before.Submitted + after.Submitted,
                    Rejected = before.Rejected + after.Rejected, EmptyPlans = before.EmptyPlans + after.EmptyPlans,
                    End = before.End == OriginalAiWorkEnd.AnalysisIncomplete || after.End == OriginalAiWorkEnd.AnalysisIncomplete ? OriginalAiWorkEnd.AnalysisIncomplete : before.End,
                    MilitaryReason = after.Submitted > 0 || after.MilitaryReason is MilitaryDecisionReason.Withdrawing or MilitaryDecisionReason.AnalysisIncomplete
                        ? after.MilitaryReason : before.MilitaryReason,
                    GroupEvaluations = before.GroupEvaluations + after.GroupEvaluations,
                    GroupBaselines = before.GroupBaselines + after.GroupBaselines,
                    GroupImprovements = before.GroupImprovements + after.GroupImprovements,
                    ElapsedTimestampTicks = before.ElapsedTimestampTicks + after.ElapsedTimestampTicks,
                    AllocatedBytes = before.AllocatedBytes + after.AllocatedBytes, PathQueries = before.PathQueries + after.PathQueries,
                    PathSearches = before.PathSearches + after.PathSearches, PathExpanded = before.PathExpanded + after.PathExpanded,
                    PathEndpointExpanded = before.PathEndpointExpanded + after.PathEndpointExpanded };
            }
        }
        if (progressed is int id) batch.State = batch.State with { LastCorps = batch.State.LastCorps.SetItem(seat.ForceId, id) };
    }
    internal static OriginalAiWorkReport Run(SimulationEngine engine, ScenarioSessionDefinition definition, CorpsState corps,
        CommandContext context, MilitaryTurnBatch batch, List<DomainEvent> events, MilitaryPlanningPass pass = MilitaryPlanningPass.Complete) => definition.MilitaryRules is null
            ? new(corps.Id, OriginalAiWork.Attack, OriginalAiWorkEnd.NoCandidates, 0, 0)
                { Executor = "military-campaign-v1", MilitaryReason = MilitaryDecisionReason.NoMap }
            : new MilitaryCorpsPlanner(engine, definition, corps, context, batch, events).Run(pass);
}

using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

public enum MilitaryDecisionReason
{
    NoMap, NoSource, NoGroup, NoTarget, Unreachable, NotWorthwhile, AwaitingAssembly,
    ExistingCommitment, Consolidating, Withdrawing, Committed, AnalysisIncomplete, PublicRejected
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
        int? progressed = null;
        int remaining = ordered.Length;
        foreach (var corps in ordered)
        {
            if (engine.Snapshot.Simulation.Boundary != SimulationBoundary.Stable) break;
            var context = new CommandContext(corps.ControlMode == CorpsControlMode.Automatic ? CorpsControl.ControllerId(corps.Id) : seat.ControllerId);
            batch.BeginCorps(seat.ForceId, remaining--);
            var report = Run(engine, definition, corps, context, batch, events);
            reports.Add(report);
            if (report.GroupEvaluations > 0 || report.Attempts > 0 || report.PathSearches > 0) progressed = corps.Id;
        }
        if (progressed is int id) batch.State = batch.State with { LastCorps = batch.State.LastCorps.SetItem(seat.ForceId, id) };
    }
    internal static OriginalAiWorkReport Run(SimulationEngine engine, ScenarioSessionDefinition definition, CorpsState corps,
        CommandContext context, MilitaryTurnBatch batch, List<DomainEvent> events) => definition.MilitaryRules is null
            ? new(corps.Id, OriginalAiWork.Attack, OriginalAiWorkEnd.NoCandidates, 0, 0)
                { Executor = "military-campaign-v1", MilitaryReason = MilitaryDecisionReason.NoMap }
            : new MilitaryCorpsPlanner(engine, definition, corps, context, batch, events).Run();
}

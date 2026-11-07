using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Presentation;

public sealed class UnitGroupCommandTargets : MilitaryTargets
{
    public ImmutableArray<UnitState> Units { get; }
    public UnitCommandKind Kind { get; }
    public UnitPolicyOverrides Overrides { get; private set; } = new();
    public override bool IsDeployment => false;
    public override MilitaryTarget Source => new(Units[0].Id, true);
    public override IReadOnlyList<MilitaryTarget> Sources { get; }
    protected override bool CanProceed => true;
    protected override uint Soldiers => Units[0].Soldiers;
    protected override int FormationId => Units[0].FormationId!.Value;
    protected override int? NavalFormationId => Units[0].NavalFormationId;
    protected override WorldPosition? Start(UnitTravel travel, IReadOnlyList<MapOccupancy> occupancy) => Units[0].Position;
    private ImmutableArray<ArrivalEstimate> _estimates = [];
    private (MilitaryTarget Target, UnitPolicyOverrides Policies, WorldPosition? Waypoint)? _estimateRequest;

    public UnitGroupCommandTargets(StrategySnapshot view, IEnumerable<int> unitIds, UnitCommandKind kind)
        : this(view, Resolve(view, unitIds, kind), kind) { }

    private UnitGroupCommandTargets(StrategySnapshot view, ImmutableArray<UnitState> units, UnitCommandKind kind)
        : base(view, view.MilitaryRules!, units[0].OwnerId, null, units[0].Id, kind)
    {
        Units = units; Kind = kind;
        Sources = units.Select(u => new MilitaryTarget(u.Id, true)).ToArray();
        var query = new MilitaryOrderQuery(view.World, Rules);
        Candidates = Candidates.Where(pair => units.All(u => query.CanAssign(u.Id, OrderFor(pair.Key))))
            .ToImmutableDictionary();
        ClearCells = Candidates.Keys.SelectMany(t => t.IsUnit
                ? Rules.GeographicFootprint(view.World.Units.Single(u => u.Id == t.Id))
                : Rules.GeographicFootprint(view.World.Settlements.Single(s => s.Id == t.Id)))
            .Concat(units.SelectMany(Rules.GeographicFootprint)).ToImmutableHashSet();
    }

    private static ImmutableArray<UnitState> Resolve(StrategySnapshot view, IEnumerable<int> ids, UnitCommandKind kind)
    {
        var selected = ids.Distinct().Order().ToArray();
        if (selected.Length == 0 || kind is not (UnitCommandKind.Attack or UnitCommandKind.March) ||
            selected.Any(id => !MapCommandMenu.Unit(view, id).Any(e => e.Id == UnitCommandTargets.CommandId(kind) && e.Enabled)))
            throw new ArgumentException("Group command is unavailable.", nameof(ids));
        return selected.Select(id => view.World.Units.Single(u => u.Id == id)).ToImmutableArray();
    }

    protected override RoutePreview Plan(MilitaryTarget target, WorldPosition? waypoint, MilitaryOrder? existingOrder = null)
    {
        var previews = Units.Select(u => PlanUnit(u, target, waypoint, Overrides.Apply(u.Policies))).ToArray();
        int failed = Array.FindIndex(previews, p => !p.Reachable);
        return (failed >= 0 ? new RoutePreview([], $"部队 {Units[failed].Id}：{previews[failed].Error}") : previews[0])
            with { UnitRoutes = previews.Select(p => p.Cells).ToImmutableArray() };
    }

    public void SetPolicy(UnitPolicyField field, bool? value)
    {
        if (!EditingPolicies) return;
        var updated = Overrides.With(field, value);
        if (Overrides == updated) return;
        Overrides = updated;
        RefreshRoute();
    }
    public override void SetPolicies(UnitPolicies policies)
    {
        if (!EditingPolicies) return;
        Overrides = new(policies.AttackApproachingEnemies, policies.AutonomousRetreat, policies.Pursue, policies.AttackAfterAction);
        RefreshRoute();
    }
    public IReadOnlyList<ArrivalEstimate> ArrivalEstimates()
    {
        if (Selected is not { } target) return [];
        var request = (target, Overrides, WaypointPosition);
        if (_estimateRequest != request)
        {
            _estimateRequest = request;
            var forecast = new MilitaryArrivalForecast(View.World, Rules);
            _estimates = Units.Select(u => forecast.EstimateOrder(u.Id, OrderFor(target), Overrides.Apply(u.Policies), WaypointPosition)).ToImmutableArray();
        }
        return _estimates;
    }
    public override ArrivalEstimate EstimateArrival()
    {
        var estimates = ArrivalEstimates();
        return estimates.FirstOrDefault(e => e.Status != ArrivalEstimateStatus.Known) ??
            estimates.MaxBy(e => e.Days) ?? new(ArrivalEstimateStatus.NotApplicable);
    }
    public override OrderUnitsCommand CreateCommand(Guid commandId, long sequence, bool confirmAttack = false) =>
        new(commandId, OwnerId, sequence, Units.Select(u => new UnitOrderRequest(u.Id,
            Overrides.Apply(u.Policies), WaypointPosition)).ToImmutableArray(), ReadyOrder()) { ConfirmAttack = confirmAttack };
}

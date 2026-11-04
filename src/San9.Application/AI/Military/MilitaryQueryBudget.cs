namespace San9.Application.AI.Military;

/// <summary>Deterministic work allowance for one shared planning batch. Cache hits pay the same
/// logical expansion cost as a cold query; wall time and diagnostic collection do not spend work.</summary>
public sealed class MilitaryQueryBudget
{
    private MilitaryQueryBudget? parent;
    private readonly int maximumSourceFields;
    private readonly int maximumDetailedRoutes;
    public int MaximumExpanded { get; }
    public int Expanded { get; private set; }
    public int RemainingExpanded => MaximumExpanded - Expanded;
    public int SourceFields { get; private set; }
    public int RemainingSourceFields => maximumSourceFields - SourceFields;
    public int DetailedRoutes { get; private set; }
    public int RemainingDetailedRoutes => maximumDetailedRoutes - DetailedRoutes;
    public bool Exhausted { get; private set; }

    public MilitaryQueryBudget(int maximumExpanded = 262144, int maximumSourceFields = 4, int maximumDetailedRoutes = 8)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumExpanded);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSourceFields);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDetailedRoutes);
        MaximumExpanded = maximumExpanded;
        this.maximumSourceFields = maximumSourceFields;
        this.maximumDetailedRoutes = maximumDetailedRoutes;
    }

    internal MilitaryQueryBudget Partition(int expanded, int sources, int routes) =>
        new(Math.Min(expanded, RemainingExpanded), Math.Min(sources, RemainingSourceFields), Math.Min(routes, RemainingDetailedRoutes)) { parent = this };

    internal MilitaryQueryBudget Slice(int remainingCorps)
    {
        int Share(int value) => value == 0 ? 0 : Math.Max(1, value / Math.Max(1, remainingCorps));
        return new(Share(RemainingExpanded), Share(RemainingSourceFields), Share(RemainingDetailedRoutes)) { parent = this };
    }

    public bool BeginSourceField()
    {
        if (SourceFields >= maximumSourceFields) { Exhausted = true; return false; }
        if (parent is not null && !parent.BeginSourceField()) { Exhausted = true; return false; }
        SourceFields++;
        return true;
    }
    public bool BeginDetailedRoute()
    {
        if (DetailedRoutes >= maximumDetailedRoutes) { Exhausted = true; return false; }
        if (parent is not null && !parent.BeginDetailedRoute()) { Exhausted = true; return false; }
        DetailedRoutes++;
        return true;
    }
    public bool Spend(int expanded)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expanded);
        int used = Math.Min(expanded, RemainingExpanded);
        if (parent is not null) { used = Math.Min(used, parent.RemainingExpanded); parent.Spend(used); }
        Expanded += used;
        if (used == expanded) return true;
        Exhausted = true;
        return false;
    }
}

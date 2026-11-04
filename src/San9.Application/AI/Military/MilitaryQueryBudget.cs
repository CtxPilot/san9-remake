namespace San9.Application.AI.Military;

/// <summary>Deterministic work allowance for one shared planning batch. Cache hits pay the same
/// logical expansion cost as a cold query; wall time and diagnostic collection do not spend work.</summary>
public sealed class MilitaryQueryBudget
{
    private readonly int maximumSourceFields;
    private readonly int maximumDetailedRoutes;
    public int MaximumExpanded { get; }
    public int Expanded { get; private set; }
    public int RemainingExpanded => MaximumExpanded - Expanded;
    public int SourceFields { get; private set; }
    public int DetailedRoutes { get; private set; }
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

    public bool BeginSourceField()
    {
        if (SourceFields >= maximumSourceFields) { Exhausted = true; return false; }
        SourceFields++;
        return true;
    }
    public bool BeginDetailedRoute()
    {
        if (DetailedRoutes >= maximumDetailedRoutes) { Exhausted = true; return false; }
        DetailedRoutes++;
        return true;
    }
    public bool Spend(int expanded)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expanded);
        int used = Math.Min(expanded, RemainingExpanded);
        Expanded += used;
        if (used == expanded) return true;
        Exhausted = true;
        return false;
    }
}

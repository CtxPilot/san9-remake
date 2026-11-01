using San9.Core.Domain;

namespace San9.Core.Simulation;

/// <summary>Periodic city income policy, shared by settlement and static forecasts.</summary>
public readonly record struct EconomicIncomeRates(int RevenuePercent, int HarvestPercent)
{
    public static EconomicIncomeRates ForForce(SimulationState simulation, ForceState? force, CompanionSettings companion) =>
        force is null || force.IsBarbarian || simulation.Controls.Any(c => c.ForceId == force.Id && c.Kind == ControllerKind.Player)
            ? new(100, 100)
            : new(companion.ComputerRevenuePercent, companion.ComputerHarvestPercent);

    public long Revenue(SettlementState city) => EconomyMath.Revenue(city, RevenuePercent);
    public long Harvest(SettlementState city) => EconomyMath.Harvest(city, HarvestPercent);
}

using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

/// <summary>Current resources and actual inbound commitments. No future arrival pays a current command.</summary>
internal static class MilitaryResourceLedger
{
    internal static long ReceivingSpace(WorldSnapshot world, SettlementState destination)
    {
        long occupied = (long)destination.Soldiers + destination.Wounded;
        occupied += world.Units.Where(u => !u.IsEmbedded && u.OwnerId == destination.OwnerId &&
            u.Order?.SettlementId == destination.Id && u.Order.Kind is MilitaryOrderKind.Return or MilitaryOrderKind.Transport)
            .Sum(u => (long)u.Soldiers + u.Wounded);
        occupied += world.Simulation.Tasks.Where(t => t.ForceId == destination.OwnerId && t.Reinforcements is not null &&
            t.Journey?.DestinationPlaceId == destination.Id).Sum(t => (long)t.Reinforcements!.Soldiers + t.Reinforcements.Wounded);
        return Math.Max(0, destination.SoldierCapacity - occupied);
    }

    internal static (long AdditionalFood, long Shortfall) DeploymentFood(WorldSnapshot world, int force, int corps,
        SettlementState source, uint soldiers, int outwardDays, int engagementDays, CompanionSettings settings)
    {
        int horizon = (int)Math.Min(720, 2L * outwardDays + Math.Max(1, engagementDays));
        var supply = FoodSupplyForecast.Capture(world, force, corps, settings);
        long extra = FoodSupplyForecast.DeploymentExpenditure(source.Soldiers, Math.Min(source.Soldiers, soldiers), horizon,
            source.Wounded, source.ConsumesFood);
        long currentNeed = FoodSupplyForecast.RequiredFood(supply.DailyConsumption, Math.Min(10, horizon), []);
        return (extra, Math.Max(0, currentNeed + extra - supply.Balance));
    }

    internal static long ReservedMoney(MilitaryCampaignState state, int corps, long? except = null) => state.Campaigns
        .Where(c => c.Id != except && c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled))
        .SelectMany(c => c.Reservations).Where(r => r.CorpsId == corps).Sum(r => r.Money);

    internal static long ReservedSoldiers(MilitaryCampaignState state, int source, long? except = null) => state.Campaigns
        .Where(c => c.Id != except && c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled))
        .SelectMany(c => c.Reservations).Where(r => r.SourceId == source).Sum(r => (long)r.Soldiers);

    internal static bool OfficerReserved(MilitaryCampaignState state, int officer, long? except = null) =>
        state.Campaigns.Any(c => c.Id != except && c.Stage is not (MilitaryCampaignStage.Completed or MilitaryCampaignStage.Cancelled) &&
            (c.ReservedOfficers.Contains(officer) || c.Reservations.Any(r => r.OfficerIds.Contains(officer))));
}

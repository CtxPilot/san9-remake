using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Core.Simulation;

/// <summary>Explicit public topology and frozen occupancy; no world, command, task or random handles.</summary>
public sealed record NavigationArrivalInput(UnitTravel Travel, WorldPosition Start, MapOccupancy Target,
    ImmutableArray<MapOccupancy> Occupancy, NavigationArrival Arrival = NavigationArrival.Range, int PreparationDays = 0,
    int MovementRemainder = 0, bool RectangularReach = false, int Range = 1,
    WorldPosition? Waypoint = null, NavigationLookahead? Lookahead = null, MapOccupancy? RelayExcludedArea = null);

public static class NavigationArrivalForecast
{
    /// <summary>Optimistic travel-time bound, ignoring obstacles and using the fastest terrain.
    /// This is not proof of reachability and must never authorize an own movement.</summary>
    public static int EarliestDays(UnitTravel travel, WorldPosition start, MapOccupancy target,
        int preparationDays = 0, int? movementRemainder = null, int maximumRange = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(preparationDays);
        if (movementRemainder is int known) ArgumentOutOfRangeException.ThrowIfNegative(known);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumRange);
        long distance = Math.Max(0, new MapOccupancy(start, travel.Shape).DistanceTo(target) - 2L * maximumRange);
        int remainder = movementRemainder ?? travel.Map.MaximumStepCost(travel.Mode) - 1;
        long cost = Math.Max(0, distance * travel.Map.MinimumStepCost(travel.Mode) - remainder);
        if (travel.DailyBudget <= 0) return cost == 0 ? preparationDays : int.MaxValue;
        return (int)Math.Min(int.MaxValue, preparationDays + (cost + travel.DailyBudget - 1) / travel.DailyBudget);
    }

    /// <summary>Conservative spatial coverage, including an unknown carried movement remainder.
    /// This bounds displacement only; it neither proves passage nor predicts an enemy order.</summary>
    public static long MaximumDistance(UnitTravel travel, int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);
        int minimum = travel.Map.MinimumStepCost(travel.Mode);
        if (minimum <= 0 || travel.DailyBudget <= 0) return 0;
        return ((long)days * travel.DailyBudget + travel.Map.MaximumStepCost(travel.Mode) - 1) / minimum;
    }

    public static ArrivalEstimate Evaluate(NavigationArrivalInput input, NavigationWorkBudget work, int maximumDays = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDays);
        ArgumentOutOfRangeException.ThrowIfNegative(input.PreparationDays);
        var position = input.Start;
        int remainder = input.MovementRemainder;
        NavigationLookahead? lookahead = input.Lookahead;
        WorldPosition? waypoint = input.Waypoint;
        var initial = Advance(input.Travel, position, input.Target, input.Arrival, input.Occupancy, 0,
            work, lookahead, input.RectangularReach, input.Range);
        if (initial.Arrived && waypoint is null) return new(ArrivalEstimateStatus.Known, input.PreparationDays);
        for (int day = input.PreparationDays + 1; day <= maximumDays; day++)
        {
            int budget = checked(input.Travel.DailyBudget + remainder);
            NavigationStep step;
            if (waypoint is { } via)
            {
                step = Advance(input.Travel, position, new(via, new(1, 1)), NavigationArrival.Point, input.Occupancy, budget, work, lookahead, excluded: input.RelayExcludedArea);
                if (step.Arrived)
                {
                    waypoint = null;
                    step = Advance(input.Travel, step.Position, input.Target, input.Arrival, input.Occupancy,
                        Math.Max(0, budget - step.Spent), work, null, input.RectangularReach, input.Range);
                }
            }
            else step = Advance(input.Travel, position, input.Target, input.Arrival, input.Occupancy,
                budget, work, lookahead, input.RectangularReach, input.Range);
            if (work.Exhausted || step.Status == NavigationStatus.Planning) return new(ArrivalEstimateStatus.Incomplete);
            if (step.Arrived && waypoint is null) return new(ArrivalEstimateStatus.Known, day);
            if (step.Status == NavigationStatus.Unreachable) return new(ArrivalEstimateStatus.Unreachable);
            // This geometry-only query repeats a fixed seed, unlike the date-dependent military forecast.
            if (step.Status == NavigationStatus.Waiting && step.Remainder == 0 && step.Spent == 0 && lookahead == step.Lookahead)
                return new(ArrivalEstimateStatus.Waiting);
            position = step.Position;
            remainder = step.Remainder;
            lookahead = step.Lookahead;
        }
        return new(ArrivalEstimateStatus.Incomplete);
    }

    // Both the full world forecast and authoritative daily movement use this same navigation operation.
    internal static NavigationStep Advance(UnitTravel travel, WorldPosition position, MapOccupancy target, NavigationArrival arrival,
        IReadOnlyList<MapOccupancy> occupancy, int budget, NavigationWorkBudget? work, NavigationLookahead? lookahead,
        bool rectangularReach = false, int maximumRange = 1, Func<WorldPosition, int>? range = null, int lookaheadCells = 25,
        ulong randomSeed = 0, bool continueApproach = false, int mobility = 20, MapOccupancy? excluded = null, bool requireTargetClearance = false) =>
        travel.Map.Navigate(position, target, arrival, travel.Shape, travel.Mode, occupancy, budget,
            range: range ?? (_ => maximumRange), rectangularReach: rectangularReach, maximumRange: maximumRange,
            work: work, lookahead: lookahead, lookaheadCells: lookaheadCells, randomSeed: randomSeed, continueApproach: continueApproach, mobility: mobility, excluded: excluded,
            requireTargetClearance: requireTargetClearance);
}

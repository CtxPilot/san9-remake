using San9.Core.Domain;

namespace San9.Core.Simulation;

/// <summary>Administrative affiliation follows current location; service place is not physical presence.</summary>
internal static class PersonnelLocations
{
    internal static WorldPosition Position(WorldMutation world, PersonnelRulebook places, int id) =>
        world.Settlements.TryGetValue(id, out var settlement) && settlement.Facility is { } facility
            ? facility.Position : places.Places[id].Position;

    internal static PersonnelRoute Route(WorldMutation world, PersonnelRulebook places, int origin, int destination,
        WorldPosition? fieldDestination = null) => new(Position(world, places, origin),
            fieldDestination ?? Position(world, places, destination), fieldDestination is not null);

    internal static int? Corps(IWorldQuery world, OfficerState person) => person.UnitId is int unit
        ? world.Units[unit].CorpsId : person.SettlementId is int home ? world.Settlements[home].CorpsId : null;

    internal static int? ServicePlace(WorldMutation world, OfficerState person) => person.UnitId is int unit
        ? world.Units[unit].ServicePlaceId : person.SettlementId;

    /// <summary>Captures a validated person's destination at resolution without rescheduling the outbound mission.</summary>
    internal static PersistentTask AtPerson(WorldMutation world, PersistentTask task, OfficerState person, SimulationRules rules)
    {
        var unit = person.UnitId is int id ? world.Units[id] : null;
        int place = unit is null ? (person.SettlementId ?? person.RegionId)!.Value
            : rules.Military!.RegionAt(unit.Position)!.Value;
        var position = unit?.Position ?? Position(world, rules.Personnel, place);
        var journey = task.Journey!;
        bool field = unit is not null;
        if (journey.DestinationPlaceId == place && journey.Route.Destination == position && journey.Route.FieldDestination == field)
            return task;
        return task with { Journey = journey with
        {
            DestinationPlaceId = place,
            Route = journey.Route with { Destination = position, FieldDestination = field }
        } };
    }

    internal static SettlementState TaskHome(IWorldQuery world, PersistentTask task) =>
        world.Settlements[world.Officers[task.OfficerId].SettlementId!.Value];
}

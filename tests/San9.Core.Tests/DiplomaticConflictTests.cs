using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class DiplomaticConflictTests
{
    private static WorldMutation World(int relation = 0)
    {
        MilitaryOrder Attack(int id) => new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = id };
        return WorldState.Create(new(190, 1, 1), 42, [new(1, 0), new(2, 0), new(3, 0)],
            [new(100, 1, 100, 100, 100, 70, 0, 0, 0, 0, 0), new(200, 2, 100, 100, 100, 70, 0, 0, 0, 0, 0)],
            [new(41, 1, 100, new(50, 50, 50, 50), false), new(51, 2, 200, new(50, 50, 50, 50), false), new(61, 1, 100, new(50, 50, 50, 50), false)],
            [new(10, 1, new(10, 10), 100) { FormationId = 0, ServicePlaceId = 100, Order = Attack(20),
                EngagementTargetId = 20, ContactTarget = new(20, false), ActiveEngagement = true, Waypoint = new(15, 15),
                Condition = UnitCondition.Confused, ConditionDaysRemaining = 3 },
             new(11, 1, new(11, 10), 100) { FormationId = 0, Order = Attack(20) },
             new(12, 1, new(12, 10), 100) { FormationId = 0, Order = new(MilitaryOrderKind.Move, new(40, 40), null),
                EngagementTargetId = 20, ContactTarget = new(20, false), ActiveEngagement = true, Waypoint = new(25, 25) },
             new(13, 1, new(13, 10), 100) { FormationId = 0, Order = Attack(30), ContactTarget = new(30, false), ActiveEngagement = true },
             new(20, 2, new(20, 10), 100) { FormationId = 0, ServicePlaceId = 200, Order = new(MilitaryOrderKind.AttackSettlement, null, 100) },
             new(30, 3, new(30, 10), 100) { FormationId = 0, Order = Attack(10), PassiveEngagement = true }],
            relations: relation == 0 ? [] : [new(1, 2, relation)],
            corps: [new(1, 1, null, new(0, 0)) { IsRoot = true }, new(2, 2, null, new(0, 0)) { IsRoot = true },
                new(3, 3, null, new(0, 0)) { IsRoot = true },
                new(4, 1, 41, new(0, 0)) { Delegation = new DefeatForceDelegation(2) },
                new(5, 2, 51, new(0, 0)) { Delegation = new CaptureSettlementDelegation(100) },
                new(6, 1, 61, new(0, 0)) { Delegation = new DefeatForceDelegation(3) }]).BeginMutation();
    }

    [Fact]
    public void ImprovingRelationsEndsBothSidesAttacksPreservesThirdPartiesAndReportsActualReturns()
    {
        var world = World();
        var third = world.Units[30]; var ownThird = world.Units[13]; var random = world.Simulation.RandomStreams;
        var preview = DiplomaticConflicts.Preview(world, 1, 2, null);
        Assert.Equal(new[] { 10 }, preview.Units); Assert.Equal(new[] { 4 }, preview.Corps);
        List<DomainEvent> events = [];
        ForceRelations.Change(world, 2, 1, 60, null, events);
        Assert.Equal(new(MilitaryOrderKind.Return, null, 100), world.Units[10].Order);
        Assert.Equal(new(MilitaryOrderKind.Return, null, 200), world.Units[20].Order);
        Assert.True(world.Units[10].AutonomousReturn); Assert.True(world.Units[10].PursuitResponsePending);
        Assert.Null(world.Units[10].ContactTarget); Assert.Null(world.Units[10].EngagementTargetId); Assert.Null(world.Units[10].Waypoint);
        Assert.False(world.Units[10].ActiveEngagement);
        Assert.Equal((UnitCondition.Confused, 3), (world.Units[10].Condition, world.Units[10].ConditionDaysRemaining));
        Assert.Null(world.Units[11].Order); Assert.False(world.Units[11].AutonomousReturn);
        Assert.Equal(UnitPolicyReturnOutcome.NoDestination, Assert.Single(events.OfType<UnitPolicyRespondedEvent>()).ReturnOutcome);
        Assert.Equal(MilitaryOrderKind.Move, world.Units[12].Order!.Kind); Assert.Null(world.Units[12].EngagementTargetId);
        Assert.Null(world.Units[12].ContactTarget); Assert.Equal(new WorldPosition(25, 25), world.Units[12].Waypoint);
        Assert.Equal(third, world.Units[30]); Assert.Equal(ownThird, world.Units[13]);
        Assert.IsType<GeneralDelegation>(world.Corps[4].Delegation); Assert.IsType<GeneralDelegation>(world.Corps[5].Delegation);
        Assert.IsType<DefeatForceDelegation>(world.Corps[6].Delegation);
        var peace = Assert.Single(events.OfType<DiplomaticConflictsEndedEvent>());
        Assert.Equal(new[] { 10, 20 }, peace.ReturningUnitIds); Assert.Equal(new[] { 4, 5 }, peace.ClearedCorpsIds);
        Assert.Equal(random, world.Simulation.RandomStreams);
        events.Clear(); ForceRelations.Change(world, 1, 2, 20, null, events);
        Assert.Empty(events.OfType<DiplomaticConflictsEndedEvent>());
    }

    [Theory]
    [InlineData(0, 39, false)]
    [InlineData(39, 1, true)]
    [InlineData(60, 1, true)]
    [InlineData(100, 20, true)]
    [InlineData(60, -1, false)]
    [InlineData(60, 0, false)]
    public void CleanupUsesPositiveImprovementAndPostChangeHostilityIncludingSaturation(int before, int delta, bool ends)
    {
        var world = World(before); List<DomainEvent> events = [];
        ForceRelations.Change(world, 1, 2, delta, null, events);
        Assert.Equal(ends, events.OfType<DiplomaticConflictsEndedEvent>().Any());
        Assert.Equal(ends ? MilitaryOrderKind.Return : MilitaryOrderKind.AttackUnit, world.Units[10].Order!.Kind);
    }
}

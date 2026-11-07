using System.Collections.Immutable;
using San9.Application.Persistence;
using San9.Application.Presentation;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed class UnitGroupCommandTests
{
    private static (ScenarioInitialState Definition, GameSession Session) Create()
    {
        var basis = G01ScenarioTests.Fixture();
        var definition = basis with
        {
            Settlements = basis.Settlements.Select(s => s with { Soldiers = 3000, X = s.Id == 1 ? 2 : 30, Y = 10 }).ToArray(),
            Military = new(40, 20, Enumerable.Repeat(0, 800).ToImmutableArray().SetItem(15 * 40 + 20, 1), [new(0, 10, null), new(1, null, null)],
                [new(7, 10, 0, 10000, 5, new(1, 1), TravelMode.Land)
                    { Combat = new(10, 10, 10, 10, 50), Attack = new(10, true),
                        Positions = FormationTestData.Layout(Enumerable.Repeat(BattleRank.Middle, 5).ToImmutableArray()) },
                 new(70, 10, 0, 10000, 5, new(1, 1), TravelMode.Land)],
                [new(1, new(12, 52), new(1, 1)) { Combat = new(10, 10, 10, 10, 50), Attack = new(10, false) }, new(2, new(152, 52), new(1, 1)) { Combat = new(10, 10, 10, 10, 50), Attack = new(10, false) }])
                { TransportFormations = new(70, null) },
            Information = basis.Information with { Formations = [new(7, "formation"), new(70, "transport")] }
        };
        var initial = GameSession.Start(definition, 42).Capture();
        var world = initial.World with
        {
            Units = [new(10, 1, new(37, 52), 500) { MemberPositions = ImmutableDictionary<int, FormationPosition>.Empty.Add(2, new(0, BattleRank.Middle)), FormationId = 7, CommanderId = 2, Morale = 100, Policies = new(Pursue: true) },
                     new(11, 1, new(52, 52), 500) { MemberPositions = ImmutableDictionary<int, FormationPosition>.Empty.Add(3, new(0, BattleRank.Middle)), FormationId = 7, CommanderId = 3, Morale = 100, Policies = new(AutonomousRetreat: true) }],
            Officers = initial.World.Officers.Select(p => p.Id is 2 or 3 ? p with { UnitId = p.Id + 8, SettlementId = null } : p).ToArray(),
            Simulation = initial.World.Simulation with { NextUnitId = 12 }
        };
        return (definition, GameSession.Restore(definition, initial with { World = world }));
    }

    [Fact]
    public void PreviewDoesNotCommitAndMixedPurposesOnlyExposeCommonCommands()
    {
        var (definition, session) = Create();
        var selection = new UnitGroupSelection(); int changes = 0; selection.Changed += () => changes++;
        selection.Select(session.View, [10], false);
        Assert.Equal(new[] { 11 }, selection.Preview(session.View, [11, 11, 999], false));
        Assert.Equal(new[] { 10, 11 }, selection.Preview(session.View, [11], true));
        Assert.Equal(new[] { 10 }, selection.UnitIds); Assert.Equal(1, changes);
        selection.Select(session.View, [11], true);
        Assert.Equal(new[] { "attack", "march" }, selection.Commands(session.View));
        var mixed = new StrategySnapshot(definition, session.Snapshot with
        { Units = session.Snapshot.Units.Select(u => u.Id == 11 ? u with { FormationId = 70 } : u).ToArray() });
        Assert.Empty(selection.Commands(mixed));
        var invalid = new StrategySnapshot(definition, session.Snapshot with
        { Units = session.Snapshot.Units.Select(u => u.Id == 11 ? u with { Condition = UnitCondition.Confused } : u).ToArray() });
        selection.Refresh(invalid); Assert.Equal(new[] { 10 }, selection.UnitIds);
    }

    [Fact]
    public void SharedTargetHasIndependentRoutesAndOnlyExplicitPolicyFieldsAreOverridden()
    {
        var (definition, session) = Create();
        var targets = new UnitGroupCommandTargets(session.View, [11, 10, 10], UnitCommandKind.Attack);
        Assert.True(targets.Select(new(2, false)), string.Join(",", targets.Candidates.Keys));
        Assert.True(targets.Reachable, targets.Error);
        Assert.Equal(2, targets.Routes.Count);
        Assert.Equal(new MapCell(7, 10), targets.Routes[0][0]);
        Assert.Equal(new MapCell(10, 10), targets.Routes[1][0]);
        var revision = targets.Revision; var routes = targets.Routes;
        Assert.False(targets.SetWaypoint(new(20, 15)));
        Assert.Same(routes, targets.Routes); Assert.Equal(revision, targets.Revision);
        Assert.True(targets.BeginPolicies());
        targets.SetPolicy(UnitPolicyField.AttackAfterAction, true);
        var command = targets.CreateCommand(Guid.NewGuid(), session.Snapshot.Simulation.NextSequence, true);
        Assert.Equal(new(Pursue: true, AttackAfterAction: true), command.Units[0].Policies);
        Assert.Equal(new(AutonomousRetreat: true, AttackAfterAction: true), command.Units[1].Policies);
        var before = session.Capture();
        var result = session.Execute(new("player"), command);
        Assert.True(result.Submission.Accepted, result.Submission.Rejection.ToString());
        var restored = SaveService.Decode(SaveService.Encode(session), definition);
        StateAssert.Equal(session.Capture(), restored.Capture());
        var replay = GameSession.Restore(definition, before);
        var json = System.Text.Json.JsonSerializer.Serialize<GameCommand>(command, CanonicalJson.Options);
        var copy = System.Text.Json.JsonSerializer.Deserialize<GameCommand>(json, CanonicalJson.Options)!;
        var replayed = replay.Execute(new("player"), copy);
        EventAssert.Equal(result.Events, replayed.Events); StateAssert.Equal(session.Capture(), replay.Capture());
        targets.ReturnToTargets();
        targets.BeginPolicies(); targets.SetPolicy(UnitPolicyField.AttackAfterAction, null);
        Assert.False(targets.CreateCommand(Guid.NewGuid(), 0).Units[0].Policies!.AttackAfterAction);
    }
}

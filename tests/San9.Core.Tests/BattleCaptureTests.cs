using San9.Tests;
using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class BattleCaptureTests
{
    private static WorldState World() => WorldState.Create(new(190, 1, 1), 42,
        [new(1, 100) { AdviserId = 11 }, new(2, 100)],
        [new(1, 1, 100, 100, 100, 70, 500, 0, 100, 0, 100), new(2, 2, 100, 100, 100, 70, 500, 0, 100, 0, 100)],
        [new(11, 1, null, new(70, 70, 70, 70), false) { Status = OfficerStatus.Strategist, UnitId = 0 },
         new(22, 2, null, new(70, 70, 70, 70), false) { UnitId = 1 }],
        units: [new(0, 1, new(2, 7), 100) { FormationId = 7 },
            new(1, 2, new(36, 7), 100) { FormationId = 7, Order = new(MilitaryOrderKind.Return, null, 2) }], corps: [new San9.Core.Domain.CorpsState(1, 1, null, new(10000, 10000)) { IsRoot = true }, new San9.Core.Domain.CorpsState(2, 2, null, new(10000, 10000)) { IsRoot = true }]);

    private static MilitaryRulebook Rules() => new(new(8, 3,
        Enumerable.Repeat(0, 24).ToImmutableArray(), [new(0, 1, null)],
        [new(7, 10, 0, 10000, 1, new(1, 1), TravelMode.Land)],
        [new(1, new(2, 7), new(1, 1)), new(2, new(37, 7), new(1, 1))]));

    [Theory]
    [InlineData((int)OfficerAbility.Leadership)]
    [InlineData((int)OfficerAbility.Intelligence)]
    [InlineData((int)OfficerAbility.Politics)]
    public void HighestAbilityOptionUsesEachNonMartialSpecialtyAndCurrentInjury(int specialty)
    {
        bool Capture(bool enabled, int injury)
        {
            var world = World().BeginMutation(companion: new() { UseHighestAbilityForCapture = enabled });
            var person = world.Officers[11];
            var abilities = (OfficerAbility)specialty switch
            {
                OfficerAbility.Leadership => person.BaseAbilities with { Leadership = 106 },
                OfficerAbility.Intelligence => person.BaseAbilities with { Intelligence = 106 },
                OfficerAbility.Politics => person.BaseAbilities with { Politics = 106 },
                _ => throw new ArgumentOutOfRangeException(nameof(specialty))
            };
            world.Replace(person with { BaseAbilities = abilities, Injury = injury });
            List<DomainEvent> events = [];
            bool captured = BattlePersonnel.Resolve(world, 11, new(1, false), 1, 1, BattleRank.Middle, false, false,
                PersonnelRulebook.Empty, events, true, new(2, 7), Rules());
            Assert.Equal(captured ? OfficerStatus.Captive : OfficerStatus.Strategist, world.Officers[11].Status);
            return captured;
        }
        Assert.True(Capture(false, 0));
        Assert.False(Capture(true, 0));
        Assert.True(Capture(true, 5));
    }

    [Fact]
    public void FieldCaptureClearsOldUnitAndOfficeThenReturnsWithoutChangingAllegiance()
    {
        var world = World();
        _ = new SimulationEngine(world, EconomyRulebook.Empty, PersonnelRulebook.Empty, TreasureRulebook.Empty, military: Rules());
        var mutation = world.BeginMutation();
        var events = new List<DomainEvent>();
        Assert.Throws<InvalidOperationException>(() => UnitDefeatTestHelper.Complete(mutation, 0, new(1, false), 1, new([]), events));
        Assert.True(BattlePersonnel.Resolve(mutation, 11, new(1, false), null, 1, BattleRank.Middle, false, false,
            PersonnelRulebook.Empty, events, true, new(2, 7), Rules()));
        UnitDefeatTestHelper.Complete(mutation, 0, new(1, false), 1, new([]), events);
        world.Commit(mutation);
        var captured = world.CreateSnapshot();
        var person = captured.Officers.Single(p => p.Id == 11);
        Assert.Equal(OfficerStatus.Captive, person.Status);
        Assert.Equal(1, person.OwnerId);
        Assert.Equal(1, person.UnitId);
        Assert.Null(captured.Forces.Single(f => f.Id == 1).AdviserId);
        Assert.Single(events.OfType<OfficerCapturedEvent>());
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<WorldSnapshot>(CanonicalJson.Text(captured), CanonicalJson.Options)!;
        var restored = WorldState.Restore(snapshot);
        // One serving slot plus a prisoner is valid; prisoners are not formation members.
        MilitaryMovement.Validate(snapshot, Rules());
        List<DomainEvent> Advance(WorldState state)
        {
            var candidate = state.BeginMutation();
            var result = new List<DomainEvent>();
            MilitaryMovement.ResolveArrivals(candidate, MilitaryMovement.AdvanceDay(candidate, Rules(), result), Rules(), new([]), PersonnelRulebook.Empty, result);
            state.Commit(candidate);
            return result;
        }
        Assert.Equal(CanonicalJson.Text(Advance(world)), CanonicalJson.Text(Advance(restored)));
        StateAssert.Equal(world.CreateSnapshot(), restored.CreateSnapshot());
        Assert.Empty(world.CreateSnapshot().Units);
        person = world.CreateSnapshot().Officers.Single(p => p.Id == 11);
        Assert.Equal(OfficerStatus.Captive, person.Status);
        Assert.Equal(1, person.OwnerId);
        Assert.Equal(2, person.SettlementId);
        Assert.Null(person.UnitId);
    }

    [Fact]
    public void InvalidDetentionHasNoEffectsAndSettlementCaptureUsesSameTransition()
    {
        var world = World();
        var mutation = world.BeginMutation();
        var events = new List<DomainEvent>();
        var person = mutation.Officers[11];
        Assert.Throws<ArgumentException>(() => PersonnelTransitions.Capture(mutation, 11, null, 0, PersonnelRulebook.Empty, events));
        Assert.Throws<ArgumentException>(() => PersonnelTransitions.Capture(mutation, 11, 2, 1, PersonnelRulebook.Empty, events));
        Assert.Equal(person, mutation.Officers[11]);
        Assert.Empty(events);
        PersonnelTransitions.Capture(mutation, 11, 2, null, PersonnelRulebook.Empty, events);
        UnitDefeatTestHelper.Complete(mutation, 0, new(1, false), 1, new([]), events);
        world.Commit(mutation);
        Assert.Equal(2, world.CreateSnapshot().Officers.Single(p => p.Id == 11).SettlementId);
        Assert.Null(world.CreateSnapshot().Officers.Single(p => p.Id == 11).UnitId);
    }

    [Fact]
    public void EscapeKeepsAllegianceAndOfficeWhileClearingFieldLocation()
    {
        var world = World();
        var mutation = world.BeginMutation();
        var events = new List<DomainEvent>();
        Assert.Throws<ArgumentException>(() => BattlePersonnel.Resolve(mutation, 11, new(1, false), 2, 1, BattleRank.Front,
            false, false, PersonnelRulebook.Empty, events, true, new(2, 7), Rules()));
        Assert.Empty(events);
        Assert.Empty(mutation.Simulation.RandomStreams);
        Assert.False(BattlePersonnel.Resolve(mutation, 11, new(1, false), 1, 1, BattleRank.Middle, true, false,
            PersonnelRulebook.Empty, events, true, new(2, 7), Rules()));
        UnitDefeatTestHelper.Complete(mutation, 0, new(1, false), 1, new([]), events);
        world.Commit(mutation);
        var person = world.CreateSnapshot().Officers.Single(p => p.Id == 11);
        Assert.Equal(OfficerStatus.Strategist, person.Status);
        Assert.Equal(1, person.OwnerId);
        Assert.Equal(1, person.SettlementId);
        Assert.Null(person.UnitId);
        Assert.Equal(11, world.CreateSnapshot().Forces.Single(f => f.Id == 1).AdviserId);
        Assert.Single(events.OfType<OfficerEscapedBattleEvent>());
    }

    [Theory]
    [InlineData(true, 100)]
    [InlineData(false, 20)]
    [InlineData(false, 100)]
    public void CaptureOutcomeAndTerminationRepeatWithTheSameExternalConfigurationAfterStableRestore(bool hasRetreatCity, int bonus)
    {
        var world = World();
        var engine = new SimulationEngine(world, EconomyRulebook.Empty, PersonnelRulebook.Empty, TreasureRulebook.Empty, military: Rules());
        var restored = WorldState.Restore(engine.CaptureStableState());
        var companion = new CompanionSettings { NoRetreatCaptureBonus = bonus, UseHighestAbilityForCapture = true };
        List<DomainEvent> Resolve(WorldState state)
        {
            var mutation = state.BeginMutation(companion: companion);
            var events = new List<DomainEvent>();
            BattlePersonnel.Resolve(mutation, 11, new(1, false), 1, 1, BattleRank.Middle, false, false,
                PersonnelRulebook.Empty, events, hasRetreatCity, new(2, 7), Rules());
            UnitDefeatTestHelper.Complete(mutation, 0, new(1, false), 1, new([]), events);
            state.Commit(mutation);
            return events;
        }
        Assert.Equal(CanonicalJson.Text(Resolve(world)), CanonicalJson.Text(Resolve(restored)));
        StateAssert.Equal(world.CreateSnapshot(), restored.CreateSnapshot());
        Assert.Equal(hasRetreatCity || bonus < 100,
            world.CreateSnapshot().Simulation.RandomStreams.Any(s => s.Name == "battle-personnel-capture"));
    }
}

using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Core.Tests;

public sealed partial class IntelligenceTacticTests
{
    [Theory]
    [InlineData(TacticEffect.Illusion)]
    [InlineData(TacticEffect.Sorcery)]
    public void FailedPrimaryDoesNotSuppressOtherAreaResults(TacticEffect effect)
    {
        MilitaryRulebook military = null!;
        var engine = Engine(effect, area: true, captureRules: value => military = value);
        var world = WorldState.Restore(engine.CaptureStableState()).BeginMutation();
        List<DomainEvent> events = [];
        IntelligenceTactics.Resolve(world, new(0, false), world.Officers[11], military.Tactics[9000],
            new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 1 }, 0, new() { Military = military }, events);

        Assert.Contains(events.OfType<IntelligenceEffectResolvedEvent>(), e => e.TargetId == 1 && e.Effect == 0);
        Assert.Contains(events.OfType<IntelligenceEffectResolvedEvent>(), e => e.TargetId == 2 && e.Effect == 10);
        Assert.DoesNotContain(events.OfType<BattleDamageEvent>(), e => e.TargetUnitId == 1);
        Assert.True(world.Units[2].Soldiers < 10000);
        Assert.Equal(10000u, world.Units[3].Soldiers);
        WorldInvariants.Validate(world);
    }

    [Theory]
    [InlineData(TacticEffect.Defection, true, false, 0, TacticPreventionReason.PassiveCounter)]
    [InlineData(TacticEffect.Illusion, true, false, 0, TacticPreventionReason.PassiveCounter)]
    [InlineData(TacticEffect.Defection, false, false, 100, TacticPreventionReason.Formation)]
    [InlineData(TacticEffect.Illusion, false, false, 100, TacticPreventionReason.Formation)]
    [InlineData(TacticEffect.Defection, true, true, 0, null)]
    [InlineData(TacticEffect.Illusion, true, true, 0, null)]
    public void SecondaryPreventionRetainsActualReasonAndSpeakerWithoutUndoingMainHit(TacticEffect effect,
        bool counter, bool bypass, int resistance, TacticPreventionReason? expected)
    {
        MilitaryRulebook military = null!;
        var engine = Engine(effect, counter: counter, bypass: bypass, formationResistance: resistance, captureRules: value => military = value);
        var world = WorldState.Restore(engine.CaptureStableState()).BeginMutation();
        world.Replace(world.Units[1] with { CommanderId = 21 });
        List<DomainEvent> events = [];
        IntelligenceTactics.Resolve(world, new(0, false), world.Officers[11], military.Tactics[9000],
            new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 1 }, 10, new() { Military = military }, events);

        Assert.True(world.Units[1].Soldiers < 10000);
        if (expected is { } reason)
        {
            var blocked = Assert.Single(events.OfType<TacticSecondaryPreventedEvent>());
            Assert.Equal(reason, blocked.Reason);
            Assert.Equal(21, blocked.ResponderId);
            Assert.Equal(11, blocked.ActorId);
            Assert.Equal(1, blocked.Occurrence.Target.Id);
            Assert.Equal(effect == TacticEffect.Defection ? UnitCondition.NoFormation : UnitCondition.Confused, blocked.Condition);
            Assert.Equal(UnitCondition.Normal, world.Units[1].Condition);
            DomainEvent roundtrip = System.Text.Json.JsonSerializer.Deserialize<DomainEvent>(
                System.Text.Json.JsonSerializer.Serialize<DomainEvent>(blocked, CanonicalJson.Options), CanonicalJson.Options)!;
            EventAssert.Equal([blocked], [roundtrip]);
        }
        else
        {
            Assert.Empty(events.OfType<TacticSecondaryPreventedEvent>());
            Assert.NotEqual(UnitCondition.Normal, world.Units[1].Condition);
        }
        WorldInvariants.Validate(world);
    }

    public static TheoryData<TacticEffect, bool, int, int, bool, bool> AreaDiplomacyCases()
    {
        var cases = new TheoryData<TacticEffect, bool, int, int, bool, bool>();
        foreach (var effect in new[] { TacticEffect.Illusion, TacticEffect.Sorcery })
        foreach (bool settlement in new[] { false, true })
        {
            cases.Add(effect, settlement, 39, 0, false, true);
            cases.Add(effect, settlement, 40, 0, false, false);
            cases.Add(effect, settlement, 80, 0, false, false);
            cases.Add(effect, settlement, 0, 1, false, false);
            cases.Add(effect, settlement, 0, -1, false, false);
            cases.Add(effect, settlement, 0, 0, true, false);
        }
        return cases;
    }

    [Theory]
    [MemberData(nameof(AreaDiplomacyCases))]
    public void AreaEffectsUseFullAutomaticEnemyEligibility(TacticEffect effect, bool settlement, int relation,
        int requestDirection, bool allied, bool affected)
    {
        MilitaryRulebook military = null!;
        var engine = Engine(effect, area: true, areaSettlement: settlement, captureRules: value => military = value);
        var world = WorldState.Restore(engine.CaptureStableState()).BeginMutation();
        world.SetRelation(new(1, 3, relation));
        if (allied) world.SetSimulation(world.Simulation with { Alliance = new(2, 1, [1, 3], 20) });
        if (requestDirection != 0)
            AttackRequests.Establish(world, requestDirection > 0 ? 1 : 3, requestDirection > 0 ? 3 : 1, 2, []);
        var previousUnit = settlement ? null : world.Units[2];
        var previousSettlement = world.Settlements[3];
        List<DomainEvent> events = [];

        IntelligenceTactics.Resolve(world, new(0, false), world.Officers[11], military.Tactics[9000],
            new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 1 }, 10, new() { Military = military }, events);

        Assert.Contains(events.OfType<IntelligenceEffectResolvedEvent>(), e => e.TargetId == 1 && !e.TargetIsSettlement);
        Assert.Equal(affected, events.OfType<IntelligenceEffectResolvedEvent>()
            .Any(e => e.TargetId == (settlement ? 3 : 2) && e.TargetIsSettlement == settlement));
        if (affected)
            Assert.True(settlement ? world.Settlements[3].Soldiers < previousSettlement.Soldiers :
                world.Units[2].Soldiers < previousUnit!.Soldiers);
        else if (settlement) Assert.Equal(previousSettlement, world.Settlements[3]);
        else Assert.Equal(previousUnit, world.Units[2]);
        WorldInvariants.Validate(world);
    }
}

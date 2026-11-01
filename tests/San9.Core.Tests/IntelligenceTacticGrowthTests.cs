using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Core.Tests;

public sealed partial class IntelligenceTacticTests
{
    [Theory]
    [InlineData(TacticEffect.Illusion, 0, 0, false)]
    [InlineData(TacticEffect.Sorcery, 0, 0, false)]
    [InlineData(TacticEffect.Illusion, 0, 10, false)]
    [InlineData(TacticEffect.Sorcery, 0, 10, false)]
    [InlineData(TacticEffect.Illusion, 10, 0, false)]
    [InlineData(TacticEffect.Sorcery, 10, 0, false)]
    [InlineData(TacticEffect.Illusion, 3, 3, false)]
    [InlineData(TacticEffect.Sorcery, 3, 3, false)]
    [InlineData(TacticEffect.Illusion, 10, 10, false)]
    [InlineData(TacticEffect.Sorcery, 10, 10, false)]
    [InlineData(TacticEffect.Illusion, 10, 10, true)]
    [InlineData(TacticEffect.Sorcery, 10, 10, true)]
    public void AreaSourceGrowthIsAwardedOnceOnlyWhenAnyTargetHasAnEffect(TacticEffect effect,
        int primaryEffect, int secondaryEffect, bool secondarySettlement)
    {
        MilitaryRulebook military = null!;
        var engine = Engine(effect, area: true, areaSettlement: secondarySettlement, captureRules: value => military = value);
        var world = WorldState.Restore(engine.CaptureStableState()).BeginMutation();
        var actor = world.Officers[11];
        world.Replace(actor with { BaseAbilities = actor.BaseAbilities with { Intelligence = 90 } });
        world.Replace(world.Officers[21] with { LearnedTactics = [9100] });
        if (!secondarySettlement)
        {
            var defender = world.Officers[31];
            world.Replace(defender with
            {
                BaseAbilities = defender.BaseAbilities with { Intelligence = secondaryEffect == 0 ? 1000 : 0 },
                TacticProficiencies = defender.TacticProficiencies.SetItem(7, secondaryEffect == 3 ? 2000 : 0),
                LearnedTactics = [9100]
            });
        }
        ulong seed = TestSeeds.StateForRoll(0);
        world.SetSimulation(world.Simulation with { RandomStreams =
            [new("military-tactic-growth", seed), new("military-tactic-learning", seed)] });
        List<DomainEvent> events = [];

        IntelligenceTactics.Resolve(world, new(0, false), world.Officers[11], military.Tactics[9000],
            new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 1 }, primaryEffect, new() { Military = military }, events);

        var results = events.OfType<IntelligenceEffectResolvedEvent>().ToArray();
        Assert.Equal(2, results.Length);
        Assert.Contains(results, e => e.TargetId == 1 && !e.TargetIsSettlement && e.Effect == primaryEffect);
        Assert.Contains(results, e => e.TargetId == (secondarySettlement ? 3 : 2) &&
            e.TargetIsSettlement == secondarySettlement && e.Effect == secondaryEffect);
        bool hasEffect = primaryEffect > 0 || secondaryEffect > 0;
        Assert.Equal(hasEffect ? 503 : 500, world.Officers[11].TacticProficiencies[7]);
        var growth = events.OfType<TacticProficiencyGrownEvent>().Where(e => e.OfficerId == 11).ToArray();
        var abilities = events.OfType<OfficerAbilityGrownEvent>().Where(e => e.OfficerId == 11).ToArray();
        if (hasEffect)
        {
            Assert.Equal(3, Assert.Single(growth).Current - growth[0].Previous);
            Assert.Equal("intelligence", Assert.Single(abilities).Ability);
            Assert.Equal(91, world.Officers[11].BaseAbilities.Intelligence);
        }
        else
        {
            Assert.Empty(growth);
            Assert.Empty(abilities);
            Assert.DoesNotContain(events, e => e is BattleDamageEvent or BattleConditionChangedEvent or TacticLearnedEvent);
        }
        var abilityRandom = new StableRandom(seed);
        if (hasEffect) abilityRandom.NextUInt64();
        Assert.Equal(abilityRandom.State, world.Simulation.RandomStreams.Single(s => s.Name == "military-tactic-growth").State);
        // Each surviving effective target retains its own ordinary learning attempt.
        var learningRandom = new StableRandom(seed);
        if (primaryEffect > 0) learningRandom.NextUInt64();
        if (secondaryEffect > 0 && !secondarySettlement) learningRandom.NextUInt64();
        Assert.Equal(learningRandom.State, world.Simulation.RandomStreams.Single(s => s.Name == "military-tactic-learning").State);
        WorldInvariants.Validate(world);
    }
}

using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Core.Domain;

namespace San9.Application.Tests;

public sealed class MilitaryGroupEvaluationTests
{
    [Fact]
    public void AdaptedPhysicalTacticsAreAConstraintEvenWhenOtherSkillsAreStronger()
    {
        var (rules, people) = Fixture();
        var result = Evaluate(rules, people, [new(1)]);
        Assert.NotNull(result.Best);
        Assert.All(result.Best.Members, p => Assert.Equal(1, p.TacticId));
        var withoutAdapted = people.Select(p => p with { Tactics = p.Tactics.Where(t => t.Id != 1).ToImmutableArray() }).ToArray();
        Assert.All(Evaluate(rules, withoutAdapted, [new(1)]).Best!.Members, p => Assert.Equal(2, p.TacticId));
        var naval = Evaluate(rules, people, [new(1, 3)], position: new(7, 7));
        Assert.All(naval.Best!.Members, p => Assert.Equal(3, p.TacticId));
    }

    [Fact]
    public void LowBudgetReturnsExistingCandidateAndLeftRightDoesNotExpandTheSearch()
    {
        var (rules, people) = Fixture();
        var low = Evaluate(rules, people, [new(1), new(2)], evaluations: 1);
        Assert.Equal(1, low.Evaluations);
        Assert.NotNull(low.Best);
        Assert.True(low.Incomplete);
        var none = Evaluate(rules, people, [new(1), new(2)], evaluations: 0);
        Assert.Null(none.Best);
        Assert.True(none.Incomplete);
        var ordinary = Evaluate(rules, people, [new(1), new(2)]);
        var reordered = Evaluate(rules, people.Reverse().ToArray(), [new(2), new(1)]);
        Assert.Equal(ordinary.Best!.FormationId, reordered.Best!.FormationId);
        Assert.Equal(ordinary.Best.Score, reordered.Best.Score);
        Assert.Equal(ordinary.Best.Members.ToArray(), reordered.Best.Members.ToArray());
        var formations = rules.Formations.Values.Select(f => f with { Positions = f.Positions.Select(p => p with { Column = 4 - p.Column }).ToImmutableArray() }).ToArray();
        var mirrored = new MilitaryRulebook(new(2, 2, [0, 0, 1, 1], [new(0, 1, null), new(1, null, 1) { IsWater = true }],
            formations.ToImmutableArray(), []) { Tactics = rules.Tactics.Values.ToImmutableArray(), BasicNavalFormationId = 3 });
        var otherColumns = Evaluate(mirrored, people, [new(1), new(2)]);
        Assert.Equal(ordinary.Best.Score, otherColumns.Best!.Score);
        Assert.Equal(ordinary.PositionOptions, otherColumns.PositionOptions);
    }

    [Fact]
    public void ActualTroopCapacityMoneyAndUnavailableOfficersLimitTheGroup()
    {
        var (rules, people) = Fixture();
        var rich = Evaluate(rules, people, [new(1), new(2)]);
        Assert.Equal(5000u, rich.Best!.Soldiers);
        Assert.Equal(1, rich.Best.CommanderId);
        var busy = people.Select(p => p with { Officer = p.Officer with { Acted = true } }).ToArray();
        Assert.Null(Evaluate(rules, busy, [new(1)]).Best);
        Assert.Null(new MilitaryGroupEvaluator(rules).Search(new(7, 100), people, [new(1)], 10, 90000, 0, new(2, 2), MilitaryGroupRole.Field).Best);
    }

    [Fact]
    public void PublicEnemyEquipmentCanImproveProtectionWhileUnknownEquipmentKeepsAGenericOption()
    {
        var (original, people) = Fixture();
        var infantry = original.Tactics[1] with { Power = 1 };
        var counter = new TacticRule(4, 7, TacticEffect.CounterConfusion, 0, 0, 0, true, false, [1, 2]);
        var confusion = new TacticRule(5, 7, TacticEffect.Confusion, 1, 0, 0, true, false, [1, 2]);
        var rules = new MilitaryRulebook(new(2, 2, [0, 0, 1, 1], [new(0, 1, null), new(1, null, 1) { IsWater = true }],
            original.Formations.Values.ToImmutableArray(), [])
            { Tactics = [infantry, original.Tactics[2], original.Tactics[3], counter, confusion], BasicNavalFormationId = 3 });
        var person = people[0] with { Abilities = new(90, 100, 60, 99), Tactics = [infantry, counter],
            Officer = people[0].Officer with { LearnedTactics = ImmutableSortedSet.Create(1, 4) } };
        MilitaryGroupOption Choose(MilitaryKnowledge<int?> knowledge) => new MilitaryGroupEvaluator(rules).Search(new(7, 100), [person],
            [new(1)], 10, 10000, 1000, new(2, 2), MilitaryGroupRole.Field, [new(999, knowledge)]).Best!;
        Assert.Equal(1, Assert.Single(Choose(MilitaryKnowledge<int?>.Unknown).Members).TacticId);
        Assert.Equal(4, Assert.Single(Choose(MilitaryKnowledge<int?>.Known(5)).Members).TacticId);
    }

    private static MilitaryGroupEvaluation Evaluate(MilitaryRulebook rules, IReadOnlyList<MilitaryOfficerCandidate> people,
        MilitaryFormationPair[] alternatives, int evaluations = 32, WorldPosition? position = null) =>
        new MilitaryGroupEvaluator(rules).Search(new(7, 100), people, alternatives, 10, 90000, 1000,
            position ?? new(2, 2), MilitaryGroupRole.Field, maximumEvaluations: evaluations);

    private static (MilitaryRulebook, MilitaryOfficerCandidate[]) Fixture()
    {
        ImmutableArray<FormationPosition> positions = [new(0, BattleRank.Front), new(2, BattleRank.Rear)];
        var infantry = new FormationRule(1, 10, 100, 5000, 2, new(1, 1), TravelMode.Land)
            { Positions = positions, Combat = new(30, 20, 10, 20, 10), Tactics = new(100, 1, [0], []) };
        var cavalry = infantry with { Id = 2, MovementBudget = 20, Tactics = new(100, 1, [1], []) };
        var ship = infantry with { Id = 3, TravelMode = TravelMode.Water, Positions = positions, Tactics = new(100, 1, [4], []) };
        TacticRule[] tactics = [new(1, 0, TacticEffect.Martial, 10, 0, 0, true, true, [1, 2]),
            new(2, 1, TacticEffect.Martial, 100, 0, 0, true, true, [1, 2]), new(3, 4, TacticEffect.Martial, 20, 0, 0, true, true, [3])];
        var rules = new MilitaryRulebook(new(2, 2, [0, 0, 1, 1], [new(0, 1, null), new(1, null, 1) { IsWater = true }],
            [infantry, cavalry, ship], []) { Tactics = tactics.ToImmutableArray(), BasicNavalFormationId = 3 });
        var people = Enumerable.Range(1, 3).Select(id => new MilitaryOfficerCandidate(new(id, 7, 10, new(90, 60, 60, 100 - id), false)
        {
            LearnedTactics = ImmutableSortedSet.Create(1, 2, 3), TacticProficiencies = [100, 900, 0, 0, 300, 0, 0, 0, 0]
        }, new(90, 60, 60, 100 - id), tactics.ToImmutableArray())).ToArray();
        return (rules, people);
    }
}

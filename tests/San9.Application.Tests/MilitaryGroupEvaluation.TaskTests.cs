using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Core.Domain;

namespace San9.Application.Tests;

public sealed partial class MilitaryGroupEvaluationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void SiegeBaselineIncludesHighIdentityTechnologyWithAnyImprovementAllowance(int improvements)
    {
        var (original, people) = Fixture();
        var tower = original.Formations[1] with { Id = 900, RequiredTacticId = 400, Combat = new(10, 100, 100, 20, 10) };
        var technology = new TacticRule(400, 5, TacticEffect.FormationTechnology, 0, 0, 0, false, false, [900]);
        var basic = Enumerable.Range(10, 12).Select(id => original.Formations[1] with { Id = id }).ToArray();
        var rules = new MilitaryRulebook(new(2, 2, [0, 0, 1, 1], [new(0, 1, null), new(1, null, 1) { IsWater = true }],
            basic.Append(tower).ToImmutableArray(), []) { Tactics = [technology] });
        people = people.Select(p => p with { Tactics = [], Officer = p.Officer with
            { LearnedTactics = p.Officer.Id == 3 ? [400] : [], BaseAbilities = new(90, 50, 50, 50) } }).ToArray();
        var result = Evaluator(rules).Search(new(7, 100), people,
            basic.Select(f => new MilitaryFormationPair(f.Id)).Append(new(900)), 10, 5000, 1000, new(2, 2), MilitaryGroupRole.Siege,
            maximumImprovements: improvements);
        Assert.Equal(900, result.Baseline!.FormationId);
        Assert.Equal(900, result.Best!.FormationId);
        Assert.Contains(result.Best.Members, m => m.OfficerId == 3);
        Assert.InRange(result.Improvements, 0, improvements);
        Assert.True(result.BaselineEvaluations >= basic.Length + 1);
    }

    [Theory]
    [InlineData(MilitaryGroupRole.Siege, 2)]
    [InlineData(MilitaryGroupRole.Field, 1)]
    public void TargetEligibilityPrecedesPhysicalFormationAdaptation(MilitaryGroupRole role, int expectedTactic)
    {
        var (original, people) = Fixture();
        var infantry = original.Tactics[1] with { Power = 10000, AffectsFacilities = false };
        var ranged = original.Tactics[2] with { Category = 3, Power = 10, AffectsFacilities = true };
        var rules = new MilitaryRulebook(new(2, 2, [0, 0, 1, 1], [new(0, 1, null), new(1, null, 1) { IsWater = true }],
            original.Formations.Values.ToImmutableArray(), []) { Tactics = [infantry, ranged] });
        people = people.Select(p => p with { Tactics = [infantry, ranged] }).ToArray();
        var result = Evaluator(rules).Search(new(7, 100), people, [new(1)], 10, 5000, 1000, new(2, 2), role, maximumImprovements: 0);
        Assert.All(result.Best!.Members, m => Assert.Equal(expectedTactic, m.TacticId));
    }

    [Fact]
    public void MissingApplicableOffenseDoesNotEquipAnImpossibleAttack()
    {
        var (original, people) = Fixture();
        var melee = original.Tactics[1] with { AffectsFacilities = false };
        var rules = new MilitaryRulebook(new(2, 2, [0, 0, 1, 1], [new(0, 1, null), new(1, null, 1) { IsWater = true }],
            original.Formations.Values.ToImmutableArray(), []) { Tactics = [melee] });
        var result = Evaluator(rules).Search(new(7, 100), people.Select(p => p with { Tactics = [melee] }).ToArray(),
            [new(1)], 10, 5000, 1000, new(2, 2), MilitaryGroupRole.Siege, maximumImprovements: 0);
        Assert.NotNull(result.Best);
        Assert.All(result.Best.Members, m => Assert.Null(m.TacticId));
        Assert.Single(result.Best.Members);
    }

    [Fact]
    public void CommanderPrecedenceAndPersonalCapacityAreIncludedBeforeScoring()
    {
        var (rules, people) = Fixture();
        var ruler = people[0] with { Officer = people[0].Officer with { Status = OfficerStatus.Ruler } };
        var general = people[1] with { Officer = people[1].Officer with { OfficeId = 9 } };
        var evaluator = new MilitaryGroupEvaluator(rules, new([new(9, 20000, 0)]),
            new Dictionary<int, uint> { [4] = 1000 }, CompanionSettings.Default);
        var result = evaluator.Search(new(7, 100) { LeaderId = ruler.Officer.Id, RankId = 4 }, [ruler, general],
            [new(1)], 10, 5000, 1000, new(2, 2), MilitaryGroupRole.Field, maximumImprovements: 0);
        Assert.Equal(general.Officer.Id, result.Best!.CommanderId);
        Assert.Equal(5000u, result.Best.Soldiers);
        Assert.DoesNotContain(result.Best.Members, m => m.OfficerId == ruler.Officer.Id);
    }
}

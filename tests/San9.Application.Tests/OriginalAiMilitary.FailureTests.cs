using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed partial class OriginalAiMilitaryTests
{
    [Fact]
    public void RejectedDeploymentYieldsToAnotherSourceAndCanRetryAfterItsSubmission()
    {
        var basis = Fixture(4);
        var content = basis with
        {
            Settlements = [.. basis.Settlements, basis.Settlements[0] with { Id = 3, X = 7 }],
            RegionCityIds = new(basis.RegionCityIds) { [3] = 1 },
            RegionNameKeys = new(basis.RegionNameKeys) { [3] = "region.3" },
            Officers = basis.Officers.Select(p => p.State.Id is 3 or 4
                ? p with { State = p.State with { SettlementId = 3 } } : p)
                .Append(new("enemy2", basis.Officers.Single(p => p.State.Id == 100).State with { Id = 101 })).ToArray(),
            Military = basis.Military! with
                { Places = basis.Military.Places.Add(basis.Military.Places[0] with { Id = 3, Position = new(37, 7) }) }
        };
        int refusals = 0;
        var h = new Harness(content, w =>
        {
            var world = EnemyArmy(w);
            var first = world.Units.Single();
            return world with
            {
                Units = [first with { Position = new(17, 7) }, first with
                {
                    Id = 901, CommanderId = 101, Position = new(47, 7),
                    Order = new(MilitaryOrderKind.AttackSettlement, null, 3),
                    MemberRanks = ImmutableDictionary<int, BattleRank>.Empty.Add(101, BattleRank.Middle)
                }],
                Officers = world.Officers.Select(p => p.Id == 101 ? p with { SettlementId = null, UnitId = 901 } : p).ToArray(),
                Simulation = world.Simulation with { NextUnitId = 902 }
            };
        }, rejectCommand: command => command is DeployUnitCommand { SettlementId: 1 } && refusals++ == 0);

        var report = h.Run(OriginalAiWork.Deploy, 3);
        Assert.Equal(new[] { 1, 3, 1 }, h.Commands.Cast<DeployUnitCommand>().Select(c => c.SettlementId));
        Assert.Equal(1, report.Rejected);
        Assert.Equal(2, report.Submitted);
        Assert.Equal(2, h.Engine.Snapshot.Units.Count(u => u.OwnerId == 1));
    }

    [Fact]
    public void ChangedRandomTacticsRemainEligibleAfterThePreviousPlanWasRejected()
    {
        var basis = Fixture(6);
        var content = basis with
        {
            Officers = basis.Officers.Select(p => p.State.OwnerId == 1
                ? p with { State = p.State with { LearnedTactics = [901, 902] } } : p).ToArray(),
            Military = basis.Military! with
            {
                Tactics = [new(901, 8, TacticEffect.Heal, 1, 0, 0, true, false, [400]),
                    new(902, 8, TacticEffect.Heal, 1, 0, 0, true, false, [400])]
            }
        };
        int draws = 0;
        var h = new Harness(content, w => EnemyArmy(w), _ => draws++ < 3 ? 0 : 1,
            rejectCommand: command => command is DeployUnitCommand d && d.Tactics.Values.Contains(901));

        var report = h.Run(OriginalAiWork.Deploy, 2);
        Assert.Equal(2, h.Commands.Count);
        Assert.All(((DeployUnitCommand)h.Commands[0]).Tactics.Values, tactic => Assert.Equal(901, tactic));
        Assert.All(((DeployUnitCommand)h.Commands[1]).Tactics.Values, tactic => Assert.Equal(902, tactic));
        Assert.Equal(1, report.Rejected);
        Assert.Equal(1, report.Submitted);
    }
}

using System.Collections.Immutable;
using San9.Application.AI;
using San9.Application.Persistence;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed partial class OriginalAiActionsTests
{
    private static readonly (OriginalAiWork Work, AutomaticStrategyKind Kind)[] PermissionKinds =
    [
        (OriginalAiWork.FalseReport, AutomaticStrategyKind.FalseReport),
        (OriginalAiWork.Confuse, AutomaticStrategyKind.Disorder),
        (OriginalAiWork.Encourage, AutomaticStrategyKind.Inspire),
        (OriginalAiWork.Reinforce, AutomaticStrategyKind.Reinforce),
        (OriginalAiWork.Alienate, AutomaticStrategyKind.Alienate),
        (OriginalAiWork.Burn, AutomaticStrategyKind.Burn),
        (OriginalAiWork.Seize, AutomaticStrategyKind.Seize),
        (OriginalAiWork.Gossip, AutomaticStrategyKind.Gossip),
    ];
    private static readonly AutomaticStrategyPermissions NoStrategies = new()
    {
        FalseReport = false, Disorder = false, Inspire = false, Reinforce = false,
        Alienate = false, Burn = false, Seize = false, Gossip = false,
    };

    public static IEnumerable<object[]> StrategyPermissionCases() =>
        from pair in PermissionKinds
        from player in new[] { false, true }
        from restrictPlayer in new[] { false, true }
        select new object[] { pair.Work, pair.Kind, player, restrictPlayer };

    [Theory]
    [MemberData(nameof(StrategyPermissionCases))]
    public void StrategyPermissionsApplyOnlyToTheSelectedScopeBeforePlanning(OriginalAiWork work,
        AutomaticStrategyKind kind, bool player, bool restrictPlayer)
    {
        bool blocked = player == restrictPlayer;
        int draws = 0;
        var h = new Harness(change: w => w with
        {
            Simulation = w.Simulation with { Controls = [.. w.Simulation.Controls.Select(c => c.ForceId == 1
                ? c with { Kind = player ? ControllerKind.Player : ControllerKind.Automatic }
                : c.ForceId == 2 ? c with { Kind = ControllerKind.Player } : c)] },
            Units = w.Units.Select(u => work == OriginalAiWork.Confuse && u.Id == 10
                ? u with { Order = new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 20 } } : u).ToArray()
        }, draw: maximum => { draws++; Assert.False(blocked, "A prohibited strategy consumed a planning draw."); return maximum - 1; });
        var restricted = AutomaticStrategyPermissions.All.WithAllowed(kind, false);
        h.Engine.ConfigureCompanion(restrictPlayer
            ? new() { PlayerDelegatedStrategyPermissions = restricted }
            : new() { ComputerStrategyPermissions = restricted });
        var before = h.Engine.Snapshot;
        var report = h.Run(work);
        if (!blocked)
        {
            Assert.Equal(1, report.Submitted);
            return;
        }
        Assert.Equal(OriginalAiWorkEnd.Permission, report.End);
        Assert.Equal(0, report.Attempts);
        Assert.Equal(0, report.PathQueries);
        Assert.Equal(0, draws);
        Assert.Empty(h.Commands);
        StateAssert.Equal(before, h.Engine.Snapshot);
    }

    private static ScenarioInitialState PermissionContent()
    {
        var content = Fixture();
        return content with
        {
            Information = content.Information with { Corps = [.. content.Information.Corps.Select(c => c.Id == 1
                ? c with { ControlMode = CorpsControlMode.Automatic } : c)] },
            OriginalAi = content.OriginalAi with
            {
                WorkRules = content.OriginalAi.WorkRules.ToImmutableDictionary(p => p.Key, p => p.Value with
                    { OpeningWaitDays = PermissionKinds.Any(k => k.Work == p.Key) ? 0 : 10000, PeriodDays = 0 }),
                SecondaryBatches = content.OriginalAi.SecondaryBatches.ToImmutableDictionary(p => p.Key,
                    _ => PermissionKinds.Select(k => k.Work).ToImmutableArray()),
            }
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProductionSchedulerReportsProhibitedStrategiesWithoutPlanning(bool player)
    {
        var session = GameSession.Start(PermissionContent(), 42, playerForceController: player ? ControllerKind.Player : ControllerKind.Automatic,
            companionSettings: player ? new() { PlayerDelegatedStrategyPermissions = NoStrategies }
                : new() { ComputerStrategyPermissions = NoStrategies });
        if (player) session.EndControl(new("player")); else session.AdvanceXun();
        var reports = session.LastOriginalAiWork.Where(r => r.CorpsId == 1 && PermissionKinds.Any(k => k.Work == r.Work)).ToArray();
        Assert.Equal(8, reports.Length);
        Assert.All(reports, r =>
        {
            Assert.Equal(OriginalAiWorkEnd.Permission, r.End);
            Assert.Equal(0, r.Attempts);
            Assert.Equal(0, r.Submitted);
            Assert.Equal(0, r.PathQueries);
        });
    }

    [Fact]
    public void PermissionsDoNotRestrictManualCommandsOrRecallExistingMissions()
    {
        var h = new Harness();
        h.Engine.ConfigureCompanion(new() { ComputerStrategyPermissions = NoStrategies, PlayerDelegatedStrategyPermissions = NoStrategies });
        var command = new BurnCommand(Guid.NewGuid(), 1, h.Engine.Snapshot.Simulation.NextSequence, 11, 2);
        Assert.True(h.Engine.Execute(new("player"), command).Submission.Accepted);

        var basis = PermissionContent();
        var content = basis with { OriginalAi = basis.OriginalAi with
            { SecondaryBatches = basis.OriginalAi.SecondaryBatches.ToImmutableDictionary(p => p.Key,
                _ => ImmutableArray.Create(OriginalAiWork.Encourage)) } };
        var session = GameSession.Start(content, 42);
        var started = session.EndControl(new("player"));
        var mission = Assert.Single(started.Events.OfType<TaskStartedEvent>()).Task;
        var settings = new CompanionSettings { ComputerStrategyPermissions = NoStrategies, PlayerDelegatedStrategyPermissions = NoStrategies };
        session.ConfigureCompanion(settings);
        var restored = SaveService.Decode(SaveService.Encode(session), content);
        restored.ConfigureCompanion(settings);
        bool completed = false;
        for (int i = 0; i < 4; i++)
        {
            var expected = session.AdvanceXun();
            var actual = restored.AdvanceXun();
            completed |= expected.Events.OfType<TaskCompletedEvent>().Any(e => e.TaskId == mission.Id);
            EventAssert.Equal(expected.Events, actual.Events);
            StateAssert.Equal(session.Capture(), restored.Capture());
        }
        Assert.True(completed);
    }
}

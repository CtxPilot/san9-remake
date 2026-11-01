using San9.Application.AI;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed partial class OriginalAiPersonnelTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExecutionProhibitionRetainsComputerCaptivesWithoutPlanningButPreservesPlayerDelegation(bool player, bool disabled)
    {
        var original = Fixture();
        int captor = player ? 1 : 2;
        int captive = player ? 5 : 2;
        var content = original with
        {
            Information = original.Information with { Relations = [new(1, 2, 0)] },
            Officers = original.Officers.Select(p => p.State.Id == captive
                ? p with { State = p.State with { Status = OfficerStatus.Captive, SettlementId = captor } } : p).ToArray()
        };
        int draws = 0;
        var h = new Harness(content, _ => { draws++; return 0; }, corpsId: captor);
        h.Engine.ConfigureCompanion(new() { DisableComputerPrisonerExecution = disabled });
        var before = h.Engine.Snapshot;
        var progress = h.Personnel.PreparePrisoners();
        bool blocked = !player && disabled;
        Assert.Equal(blocked ? [] : new[] { captive }, progress.Execute);
        progress = h.Personnel.Dispose(progress, 2);
        if (blocked)
        {
            Assert.Equal(0, draws);
            Assert.Equal(0, progress.Attempts);
            Assert.Empty(h.Commands);
            StateAssert.Equal(before, h.Engine.Snapshot);
        }
        else
        {
            Assert.Equal(1, draws);
            Assert.Equal(captive, Assert.IsType<ExecutePrisonerCommand>(Assert.Single(h.Commands)).OfficerId);
            Assert.Equal(OfficerStatus.Dead, h.Engine.Snapshot.Officers.Single(p => p.Id == captive).Status);
        }
    }

    [Fact]
    public void ExecutionProhibitionDoesNotExcludeRulersFromOrdinaryPrisonerPlanning()
    {
        var original = Fixture();
        var content = original with
        {
            Information = original.Information with { Relations = [new(1, 2, 0)] },
            Officers = original.Officers.Select(p => p.State.Id == 1
                ? p with { State = p.State with { Status = OfficerStatus.Captive, SettlementId = 2 } } : p).ToArray()
        };
        var h = new Harness(content, corpsId: 2);
        h.Engine.ConfigureCompanion(new() { DisableComputerPrisonerExecution = true });
        Assert.Equal(new[] { 1 }, h.Personnel.PreparePrisoners().Execute);
    }

    [Fact]
    public void ExecutionProhibitionDoesNotPreventComputerPrisonerRelease()
    {
        var original = Fixture();
        var content = original with
        {
            Information = original.Information with { Relations = [new(1, 2, 100)] },
            Officers = original.Officers.Select(p => p.State.Id == 2
                ? p with { State = p.State.WithLoyalty(2, 0) with { Status = OfficerStatus.Captive, SettlementId = 2 } } : p).ToArray()
        };
        var h = new Harness(content, corpsId: 2);
        h.Engine.ConfigureCompanion(new() { DisableComputerPrisonerExecution = true });
        var progress = h.Personnel.PreparePrisoners();
        Assert.Empty(progress.Execute);
        Assert.Equal(new[] { 2 }, progress.Release);
        progress = h.Personnel.Dispose(progress, 2);
        Assert.Equal(1, progress.Submitted);
        Assert.IsType<ExileOfficerCommand>(Assert.Single(h.Commands));
        Assert.Single(h.Events.OfType<PrisonerReleasedEvent>());
    }
}

using San9.Application;
using San9.Application.Diagnostics;
using San9.Application.Persistence;
using San9.Content.Scenarios;
using San9.Core.Diagnostics;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tools.ScenarioBenchmark;

namespace San9.Headless.Tests;

public sealed class MilitaryBaselineTests
{
    [Fact]
    public void MeasuredDailyPipelineMatchesProductionAndRestoresWithoutDiagnostics()
    {
        var content = Fixture();
        var measured = GameSession.Start(content, 281, playerForceController: ControllerKind.Automatic);
        var restored = SaveService.Decode(SaveService.Encode(measured), content);
        var production = GameSession.Start(content, 281, playerForceController: ControllerKind.Automatic);
        for (int i = 0; i < 3; i++)
        {
            var started = measured.Snapshot.Clock;
            var (sample, events) = MilitaryBaseline.Advance(measured, true);
            var (control, controlEvents) = MilitaryBaseline.Advance(restored, false);
            var ordinary = production.AdvanceXun();
            Assert.Null(EventDiagnostics.Compare(events, controlEvents));
            Assert.Null(EventDiagnostics.Compare(events, ordinary.Events));
            Assert.Null(WorldDiagnostics.Compare(measured.Snapshot, production.Snapshot));
            Assert.Null(SessionDiagnostics.Compare(measured, restored));
            Assert.Equal(started.Advance().ToString(), sample.Ended);
            Assert.NotEmpty(sample.Work);
            Assert.Equal(sample.EventCount, control.EventCount);
            Assert.Equal(sample.Milliseconds, sample.PreparationMilliseconds + sample.ResolutionMilliseconds, 6);
        }
        Assert.Equal(new GameClock(190, 2, 1), measured.Snapshot.Clock);
    }

    [Fact]
    public void PercentileInterpolatesAcrossAllSamples()
    {
        Assert.Equal(28.5, MilitaryBaseline.Percentile([0, 10, 20, 30], .95), 6);
        Assert.Equal(15, MilitaryBaseline.Percentile([0, 10, 20, 30], .5));
    }

    [Fact]
    public void IncompleteControlCycleCannotBeMislabeledAsAFullXunSample()
    {
        var session = GameSession.Start(Fixture(), 281, playerForceController: ControllerKind.Automatic);
        var controller = session.Snapshot.Simulation.Controls[0];
        session.EndControl(new(controller.ControllerId));
        var before = session.Capture();
        Assert.Throws<InvalidOperationException>(() => MilitaryBaseline.Advance(session, true));
        Assert.Null(SessionDiagnostics.Compare(before, session.Capture()));
    }

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(36, 0, 1)]
    [InlineData(36, 3, -1)]
    public void InvalidMeasurementSettingsAreRejected(int xuns, int repetitions, int warmups)
    {
        var options = new MilitaryBaselineOptions { ContentPath = "content", ReportPath = "report.json", CodeRevision = "test",
            Xuns = xuns, Repetitions = repetitions, WarmupPasses = warmups };
        Assert.Throws<ArgumentException>(options.Validate);
    }

    private static ScenarioInitialState Fixture() => new(ScenarioInitialState.CurrentSchemaVersion, "military-baseline-public", 190, 1, 1, 1,
        [new(1, "force.1", 1, 50) { LeaderId = 1 }, new(2, "force.2", 2, 50) { LeaderId = 2 }],
        [new(1, "city.1", 1, 1, 1, 100, 100, 100, 50, []) { Commerce = 300, MaxCommerce = 500 },
         new(2, "city.2", 2, 2, 2, 100, 100, 100, 50, []) { Commerce = 300, MaxCommerce = 500 }], [], [])
    {
        Officers = [new("officer.1", new(1, 1, 1, new(60, 70, 80, 90), false)),
            new("officer.2", new(2, 2, 2, new(60, 70, 80, 90), false))],
        Information = new([], [], [], [new(1, 1, 1, new(1000, 2000)) { IsRoot = true },
            new(2, 2, 2, new(1000, 2000)) { IsRoot = true }], [])
    };
}

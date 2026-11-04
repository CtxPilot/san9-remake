using San9.Tools.ScenarioBenchmark;

namespace San9.Headless.Tests;

public sealed class MilitaryFoundationExperimentTests
{
    [Fact]
    public void ControlledExperimentsRetainSourcesUsePublicTimingAndKeepLegalCandidates()
    {
        var report = MilitaryFoundationExperiments.RunControlled();
        Assert.All(report.Spatial, p => Assert.True(p.MatchesScan));
        Assert.True(report.Spatial[^1].IndexChecks < report.Spatial[^1].ScanChecks);
        Assert.All(report.Spatial, p => Assert.Equal(1, p.UpdatedObjects));
        Assert.True(report.Delivery.EmptyFrontHasNoGroup);
        Assert.True(report.Delivery.OfficerReadyDay >= 10);
        Assert.True(report.Delivery.NearPassesCompanionControl);
        Assert.False(report.Delivery.RemotePassesCompanionControl);
        Assert.NotNull(report.Delivery.AssembledAttackDay);
        Assert.True(report.Groups.BaselinePrepared && report.Groups.ImprovedPrepared);
        Assert.NotEqual(report.Groups.BaselineFormation, report.Groups.ImprovedFormation);
        Assert.Equal(report.Groups.ImprovedFormation, report.Groups.ProbeChoice);
        Assert.True(report.Groups.SearchEvaluations <= report.Groups.EvaluationLimit);
        Assert.True(report.Groups.Probes.Sum(p => p.DamageCalls) <= report.Groups.ProbeDamageCallLimit);
        Assert.All(report.Groups.Probes, p => Assert.True(p.ActualFirstOutgoing > 0));
        Assert.Contains(report.Groups.Probes, p => p.FirstExchangeAbsoluteError > 0);
        Assert.False(report.Groups.OnlineProbeAdopted); // Daily morale is outside the fixed-morale arithmetic probe.
    }
}

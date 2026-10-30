using San9.Application.AI;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed partial class OriginalAiPersonnelTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(99, 2)]
    public void ResidualWorkReachesLaterCitiesAfterAnEarlierCityHasOnlyItsResident(int firstMorale, int submitted)
    {
        var c = Fixture();
        c = c with
        {
            Settlements = [c.Settlements[0] with { Morale = firstMorale, Soldiers = 10000 }, c.Settlements[1],
                c.Settlements[0] with { Id = 3, X = 5, Soldiers = 10000, Morale = 50 }],
            Officers = c.Officers.Select(p => p.State.Id is 2 or 3 ? p with { State = p.State with { SettlementId = 3 } } : p).ToArray(),
            RegionCityIds = new(c.RegionCityIds) { [3] = 3 }, RegionNameKeys = new(c.RegionNameKeys) { [3] = "later" },
            CityConnections = [new(1, 3), new(3, 1), new(3, 2), new(2, 3)]
        };
        var h = new Harness(c);
        var projection = new OriginalAiProjection(h.Engine, c, 1, new(c, null, h.Engine.Snapshot), true);
        var report = OriginalAiEconomy.Residual(1, false, 100, projection.Domestic, () => h.Engine.Snapshot,
            d => h.Engine.Execute(new("player"), new DevelopCommand(Guid.NewGuid(), 1, h.Engine.Snapshot.Simulation.NextSequence, d)).Submission.Accepted,
            (_, _) => throw new InvalidOperationException("No city needs a search in this fixture."), projection.Observe);
        Assert.Equal(submitted, report.Submitted);
        Assert.Equal(55, h.Engine.Snapshot.Settlements.Single(s => s.Id == 3).Morale);
        Assert.False(h.Engine.Snapshot.Officers.Single(p => p.Id == 1).Acted && firstMorale == 100);
    }
}

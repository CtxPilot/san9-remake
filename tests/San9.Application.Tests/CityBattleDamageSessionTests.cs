using System.Collections.Immutable;
using San9.Application.Persistence;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed class CityBattleDamageSessionTests
{
    private static ScenarioInitialState Scenario()
    {
        var combat = new CombatProfile(0, 0, 0, 1000, 50);
        return new(ScenarioInitialState.CurrentSchemaVersion, "city-damage", 190, 1, 1, 1,
            [new(1, "attacker", 1, 100) { LeaderId = 11 }, new(2, "defender", 2, 100) { LeaderId = 21 }],
            [new(1, "home", 1, 0, 0, 20000, 1000, 1000, 100, []),
                new(2, "target", 2, 2, 0, 10000, 1000, 1000, 100, [])
                    { Commerce = 1000, MaxCommerce = 1000, Cultivation = 1000, MaxCultivation = 1000, PublicOrder = 1000 }], [], [])
        {
            NaturalDeathsEnabled = false,
            Officers = [new("first", new(11, 1, 1, new(100, 100, 100, 100), false) { Status = OfficerStatus.Ruler }),
                new("second", new(21, 2, 2, new(100, 100, 100, 100), false) { Status = OfficerStatus.Ruler })],
            Military = new(4, 1, [0, 0, 0, 0], [new(0, 1, null)],
                [new(700, 5, 0, 100000, 1, new(1, 1), TravelMode.Land)
                    { Combat = combat, Attack = new(1, false), Positions = San9.Tests.FormationTestData.Layout([BattleRank.Middle]) }],
                [new(1, new(2, 2), new(1, 1)) { Combat = combat }, new(2, new(12, 2), new(1, 1)) { Combat = combat }]),
            Information = new([], [], [], [new(1, 1, 11, new(10000, 100000)) { IsRoot = true },
                new(2, 2, 21, new(10000, 100000)) { IsRoot = true }], []),
            OriginalAi = OriginalAiDefinition.Default with
            {
                WorkRules = OriginalAiDefinition.Default.WorkRules.ToImmutableDictionary(p => p.Key,
                    p => p.Value with { OpeningWaitDays = 100000 })
            }
        };
    }

    [Fact]
    public void PublicSiegeUsesExternalRateAndProducesIdenticalResultsAfterSaveRestore()
    {
        const int percent = 50;
        var definition = Scenario();
        var companion = new CompanionSettings { CityBattleDamagePercent = percent };
        var session = GameSession.Start(definition, 42, companionSettings: companion, playerForceIds: [1, 2]);
        Assert.True(session.Execute(new("player"), new DeployUnitCommand(Guid.NewGuid(), 1, 0, 1, [11], 10000, 700,
            new(MilitaryOrderKind.AttackSettlement, null, 2))).Submission.Accepted);
        var restored = SaveService.Decode(SaveService.Encode(session), definition);
        Assert.Equal(100, restored.Companion.CityBattleDamagePercent);
        restored.ConfigureCompanion(companion);
        var result = session.AdvanceXun();
        Assert.Contains(result.Events.OfType<BattleDamageEvent>(), e => e.SettlementId == 2);
        var losses = result.Events.OfType<CityBattleDamageEvent>().Where(e => e.CityId == 2).ToArray();
        Assert.NotEmpty(losses);
        Assert.All(losses, loss => Assert.Equal((5, 5, 5),
            (loss.PublicOrderLoss, loss.CommerceLoss, loss.CultivationLoss)));
        int expected = 1000 - 5 * losses.Length;
        var city = session.Snapshot.Settlements.Single(s => s.Id == 2);
        Assert.Equal(expected, city.PublicOrder);
        Assert.Equal(expected, city.Commerce);
        Assert.Equal(expected, city.Cultivation);
        EventAssert.Equal(result.Events, restored.AdvanceXun().Events);
        StateAssert.Equal(session.Capture(), restored.Capture());
    }
}

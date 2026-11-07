using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Content.Tests;

public sealed class ScenarioValidationContextTests
{
    [Fact]
    public void SharedValidationStillRejectsLaterInvalidPlacementsAndRuleOverrides()
    {
        var source = new ScenarioInitialState(ScenarioInitialState.CurrentSchemaVersion, "fixture", 190, 1, 1, null, [], [], [], [])
        { Military = new(2, 2, [0, 0, 0, 0], [new(0, 10, null)], [], []) };
        var context = new ScenarioValidationContext();
        source.Validate(context);
        MilitaryDefinition[] invalid =
        [
            source.Military! with { Places = [new(1, new(100, 100), new(1, 1))] },
            source.Military! with { Places = [new(1, new(1, 1), new(1, 1)) { EnabledTacticIds = [99] }] },
            source.Military! with { DefensiveTacticChance = 101 },
            source.Military! with { RegionIds = [99, 99, 99, 99] }
        ];
        foreach (var military in invalid)
        {
            var scenario = source with { Military = military };
            var expected = Record.Exception(scenario.Validate);
            var actual = Record.Exception(() => scenario.Validate(context));
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
        }
        source.Validate(context);
    }
}

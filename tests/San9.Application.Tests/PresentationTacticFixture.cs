using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Core.Domain;

namespace San9.Application.Tests;

internal static class PresentationTacticFixture
{
    internal static ScenarioInitialState WithInfantry(ScenarioInitialState source)
    {
        int width = source.Settlements.Select(s => s.X + 1).DefaultIfEmpty(1).Max();
        int height = source.Settlements.Select(s => s.Y + 1).DefaultIfEmpty(1).Max();
        return source with
        {
            Military = new(width, height, Enumerable.Repeat(0, width * height).ToImmutableArray(), [new(0, 1, null)], [],
                source.Settlements.Select(s => new BattlefieldPlace(s.Id, s.Position, new(1, 1))).ToImmutableArray())
            {
                Tactics = Enumerable.Range(0, 3).Select(id => new TacticRule(id, 0, TacticEffect.Martial, 10, 1, 0, true, false, [])).ToImmutableArray()
            },
            Information = source.Information with { Tactics = Enumerable.Range(0, 3).Select(id => new MilitaryCatalogDefinition(id, $"tactic.{id}")).ToArray() }
        };
    }

    internal static TextCatalog AddNames(TextCatalog texts)
    {
        foreach (var (id, name) in new[] { (0, "奋战"), (1, "奋斗"), (2, "奋迅") }) texts.Entries.Add($"tactic.{id}", name);
        return texts;
    }
}

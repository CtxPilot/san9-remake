using San9.Core.Domain;
using San9.Content.Packages;

namespace San9.Content.Scenarios;

/// <summary>One validation operation; rule snapshots exclude scenario-owned placements.</summary>
public sealed class ScenarioValidationContext
{
    private readonly Dictionary<MilitaryDefinition, MilitaryRulebook> _military = [];

    public MilitaryRulebook ValidateMilitary(MilitaryDefinition definition)
    {
        var shared = definition with { Places = [] };
        if (!_military.TryGetValue(shared, out var rules))
        {
            ContentStructure.Validate(shared);
            rules = new(shared);
            _military.Add(shared, rules);
        }
        return rules;
    }
}

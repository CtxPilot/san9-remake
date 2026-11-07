using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Application.Authoring;

public sealed partial class SharedDataEditorWorkspace
{
    // These fields have no world/reference dependencies. Every other change takes the scenario path.
    private bool RequiresScenarioValidation(ScenarioDefinitions draft)
    {
        if (_changedOfficers.Count != 0 || OfficerCatalog.NextOfficerId != _savedOfficers.NextOfficerId) return true;
        ScenarioDefinitions Dependencies(ScenarioDefinitions value) => value with
        {
            OfficerGrowth = _saved.OfficerGrowth,
            Information = value.Information with
            {
                Ranks = value.Information.Ranks.Select(r => r with { Name = "", Description = "" }).ToArray(),
                Offices = value.Information.Offices.Select(o => o with { Name = "", Description = "" }).ToArray(),
                Formations = value.Information.Formations.Select(f => f with { Name = "", Description = "" }).ToArray(),
                Tactics = value.Information.Tactics.Select(t => t with { Name = "", Description = "" }).ToArray()
            },
            Military = value.Military is { } military && _saved.Military is { } saved ? military with
            {
                DefensiveTacticChance = saved.DefensiveTacticChance,
                LinkRules = military.LinkRules with { KinshipBonus = saved.LinkRules.KinshipBonus,
                    Pairs = military.LinkRules.Pairs.Select(p => p with { Bonus = 0, SuppressesSiblingBonus = false }).ToImmutableArray() },
                CityInfluence = saved.CityInfluence
            } : value.Military
        };
        return !ContentComparison.Equal(Dependencies(draft), Dependencies(_saved));
    }

    private bool CatalogIdentitiesChanged(ScenarioDefinitions draft) =>
        !draft.Information.Ranks.Select(r => r.Id).Order().SequenceEqual(_saved.Information.Ranks.Select(r => r.Id).Order()) ||
        !draft.Information.Offices.Select(o => o.Id).Order().SequenceEqual(_saved.Information.Offices.Select(o => o.Id).Order()) ||
        !draft.Information.Formations.Select(f => f.Id).Order().SequenceEqual(_saved.Information.Formations.Select(f => f.Id).Order()) ||
        !draft.Information.Tactics.Select(t => t.Id).Order().SequenceEqual(_saved.Information.Tactics.Select(t => t.Id).Order());

    private static void ValidateLocalRules(ScenarioDefinitions draft)
    {
        draft.OfficerGrowth.Validate();
        if (draft.Information.Ranks.Any(r => string.IsNullOrWhiteSpace(r.Name)) ||
            draft.Information.Offices.Any(o => string.IsNullOrWhiteSpace(o.Name)) ||
            draft.Information.Formations.Any(f => string.IsNullOrWhiteSpace(f.Name)) ||
            draft.Information.Tactics.Any(t => string.IsNullOrWhiteSpace(t.Name)))
            throw new InvalidDataException("资料名称不能为空。");
        if (draft.Military is not { } military) return;
        MilitaryRulebook.ValidateDefensiveTacticChance(military.DefensiveTacticChance);
        military.LinkRules.Validate();
        military.CityInfluence?.Validate();
    }
}

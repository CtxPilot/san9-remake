using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Application.Authoring;

public sealed partial class SharedDataEditorWorkspace
{
    private CatalogIndex? _catalog, _savedCatalog;
    private CatalogIndex Catalog => Index(ref _catalog, Definitions);
    private CatalogIndex SavedCatalog => Index(ref _savedCatalog, _saved);

    private static CatalogIndex Index(ref CatalogIndex? index, ScenarioDefinitions definitions)
    {
        if (index is null || !ReferenceEquals(index.Definitions, definitions)) index = new(definitions);
        return index;
    }

    /// <summary>Derived indexes belong to one immutable definition snapshot and are rebuilt on replacement.</summary>
    private sealed class CatalogIndex
    {
        public ScenarioDefinitions Definitions { get; }
        public Dictionary<int, FormationCatalogDefinition> FormationDefinitions { get; }
        public Dictionary<int, TacticCatalogDefinition> TacticDefinitions { get; }
        public Dictionary<int, FormationRule> FormationRules { get; }
        public Dictionary<int, TacticRule> TacticRules { get; }
        public Dictionary<int, string> FormationNames { get; }
        public ImmutableArray<FormationRule> Formations { get; }
        public ImmutableArray<TacticRule> Tactics { get; }

        public CatalogIndex(ScenarioDefinitions definitions)
        {
            Definitions = definitions;
            FormationDefinitions = definitions.Information.Formations.ToDictionary(f => f.Id);
            TacticDefinitions = definitions.Information.Tactics.ToDictionary(t => t.Id);
            Formations = (definitions.Military?.Formations ?? []).Where(f => FormationDefinitions.ContainsKey(f.Id)).ToImmutableArray();
            Tactics = (definitions.Military?.Tactics ?? []).Where(t => TacticDefinitions.ContainsKey(t.Id)).ToImmutableArray();
            FormationRules = Formations.ToDictionary(f => f.Id); TacticRules = Tactics.ToDictionary(t => t.Id);
            FormationNames = FormationDefinitions.ToDictionary(p => p.Key, p => p.Value.Name);
        }
    }
}

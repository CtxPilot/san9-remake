using System.Text;
using San9.Content.Authoring;

namespace San9.Importer.San9PkTc101.Scenarios;

public static class OfficerFlagCatalogCompiler
{
    public static OfficerFlagCatalog Compile(IReadOnlyList<San9ScenarioPersonRecord> people, Func<int, string> surname)
    {
        Dictionary<string, int> defaults = [];
        // Source expansion slots have no built-in name resources; no world status filter applies.
        foreach (var person in people.Take(700))
            if (person.FlagCode < 186)
            {
                string name = surname(person.FamilyNameResourceCode);
                if (name.Length > 0 && Rune.TryGetRuneAt(name, 0, out var initial))
                    defaults.TryAdd(initial.ToString(), person.FlagCode);
            }
        var result = new OfficerFlagCatalog(OfficerFlagCatalog.CurrentSchemaVersion, defaults, Enumerable.Range(0, 186).ToArray());
        result.Validate(); return result;
    }
}

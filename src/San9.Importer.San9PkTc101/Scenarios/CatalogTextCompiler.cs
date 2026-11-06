using San9.Content.Scenarios;
using San9.Content.Text;

namespace San9.Importer.San9PkTc101.Scenarios;

/// <summary>Source message slots are references to the current catalog, never a second authored body.</summary>
public static class CatalogTextCompiler
{
    public static void BindMessages(IDictionary<string, MessageDefinition> messages, ScenarioInitialState scenario)
    {
        void Bind(int source, string property, MessageEntityKind kind, int id) => messages[$"messages.{source}"] =
            new([new MessageValue(new("property", property, Arguments: [new("entity", kind.ToString(), id)]))]);
        foreach (var rank in scenario.Information.Ranks)
        {
            Bind(1652 + 2 * rank.Id, "rank.name", MessageEntityKind.Rank, rank.Id);
            if (rank.Id != 8) Bind(7761 + rank.Id, "rank.description", MessageEntityKind.Rank, rank.Id);
        }
        foreach (var office in scenario.Information.Offices)
        {
            Bind(5065 + 2 * office.Id, "office.name", MessageEntityKind.Office, office.Id);
            Bind(7770 + office.Id, "office.description", MessageEntityKind.Office, office.Id);
        }
        foreach (var formation in scenario.Information.Formations)
        {
            Bind(5363 + 3 * formation.Id, "formation.name", MessageEntityKind.Formation, formation.Id);
            Bind(5365 + 3 * formation.Id, "formation.description", MessageEntityKind.Formation, formation.Id);
        }
        foreach (var tactic in scenario.Information.Tactics)
        {
            Bind(1219 + 3 * tactic.Id, "tactic.name", MessageEntityKind.Tactic, tactic.Id);
            Bind(1221 + 3 * tactic.Id, "tactic.description", MessageEntityKind.Tactic, tactic.Id);
        }
    }
}

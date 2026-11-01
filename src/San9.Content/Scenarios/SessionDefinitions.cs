using System.Collections.Immutable;
using System.Text.Json.Serialization;
using San9.Content.Text;

namespace San9.Content.Scenarios;

/// <summary>Display identity for a person added to this world. All gameplay values belong to OfficerState.</summary>
public sealed record SessionOfficerDefinition(int Id, string Surname, string Name, string CourtesyName, string Biography, string? PortraitKey)
{
    [JsonIgnore] public string NameKey => $"created.{Id}.name";
    [JsonIgnore] public string? CourtesyNameKey => CourtesyName.Length > 0 ? $"created.{Id}.courtesy" : null;
    [JsonIgnore] public string? BiographyKey => Biography.Length > 0 ? $"created.{Id}.biography" : null;
}
public sealed record SessionForceDefinition(int Id, int FounderId);

/// <summary>Only definitions absent from the base scenario; no authoring library or initial gameplay state.</summary>
public sealed record SessionDefinitions
{
    [JsonRequired] public ImmutableArray<SessionOfficerDefinition> Officers { get; init; } = [];
    [JsonRequired] public ImmutableArray<SessionForceDefinition> Forces { get; init; } = [];
    public static SessionDefinitions Empty { get; } = new();

    public void Validate()
    {
        if (Officers.IsDefault || Forces.IsDefault || Officers.Any(p => p is null || p.Id < 0 || p.Surname is null ||
            string.IsNullOrWhiteSpace(p.Name) || p.CourtesyName is null || p.Biography is null || p.PortraitKey is not null && string.IsNullOrWhiteSpace(p.PortraitKey)) ||
            Forces.Any(f => f is null || f.Id < 0 || !Officers.Any(p => p.Id == f.FounderId)) ||
            Officers.Select(p => p.Id).Distinct().Count() != Officers.Length || Forces.Select(f => f.Id).Distinct().Count() != Forces.Length)
            throw new InvalidDataException("Invalid session definitions.");
    }

    public void Validate(ScenarioInitialState source)
    {
        Validate();
        if (Officers.Any(p => source.Officers.Any(o => o.State.Id == p.Id)) || Forces.Any(f => source.Forces.Any(o => o.Id == f.Id)))
            throw new InvalidDataException("Session definitions collide with base content.");
    }

    public TextCatalog AddText(TextCatalog source)
    {
        if (Officers.IsEmpty) return source;
        var entries = new Dictionary<string, string>(source.Entries);
        var profiles = new Dictionary<int, MessagePersonProfile>(source.MessagePeople);
        foreach (var p in Officers)
        {
            entries[p.NameKey] = MessageMarkup.Escape(p.Surname + p.Name);
            string? Add(string suffix, string value)
            {
                if (value.Length == 0) return null;
                string key = $"created.{p.Id}.{suffix}"; entries[key] = MessageMarkup.Escape(value); return key;
            }
            profiles[p.Id] = new(Add("surname", p.Surname), Add("given", p.Name), p.PortraitKey);
            Add("courtesy", p.CourtesyName); Add("biography", p.Biography);
        }
        return source with { Entries = entries, MessagePeople = profiles };
    }

    public Dictionary<string, string> AddPortraits(IReadOnlyDictionary<string, string> source)
    {
        var roles = new Dictionary<string, string>(source);
        foreach (var p in Officers)
            foreach (string role in new[] { $"catalog-person-{p.Id}", $"information-person-{p.Id}" })
                if (p.PortraitKey is { } portrait) roles[role] = portrait;
                else roles.Remove(role);
        return roles;
    }
}

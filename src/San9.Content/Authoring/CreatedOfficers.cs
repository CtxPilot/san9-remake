using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using San9.Content.Packages;
using San9.Core.Domain;

namespace San9.Content.Authoring;

/// <summary>Portable authored identity independent of a running game.</summary>
public sealed record CreatedOfficer(Guid Id, string Name)
{
    [JsonRequired] public string Surname { get; init; } = "";
    [JsonIgnore] public string FullName => Surname + Name;
    [JsonRequired] public string CourtesyName { get; init; } = "";
    [JsonRequired] public string Biography { get; init; } = "";
    [JsonRequired] public string? PortraitKey { get; init; }
    [JsonRequired] public int FlagGlyph { get; init; } = -1;
    [JsonRequired] public OfficerAbilities Abilities { get; init; } = new(50, 50, 50, 50);
    [JsonRequired] public bool IsFemale { get; init; }
    [JsonRequired] public int BirthYear { get; init; } = 170;
    [JsonRequired] public int AppearanceYear { get; init; } = 184;
    [JsonRequired] public int LifespanTier { get; init; } = 7;
    [JsonRequired] public int Personality { get; init; } = 1;
    [JsonRequired] public int Righteousness { get; init; } = 10;
    [JsonRequired] public int Ambition { get; init; } = 10;
    [JsonRequired] public int Affinity { get; init; } = 75;
    [JsonRequired] public Guid? ParentId { get; init; }
    [JsonRequired] public int? ScenarioParentId { get; init; }
    [JsonRequired] public ImmutableArray<int> Proficiencies { get; init; } = [0, 0, 0, 0, 0, 0, 0, 0, 0];
    [JsonRequired] public ImmutableArray<int> MasteredCategories { get; init; } = [];
    [JsonRequired] public ImmutableSortedSet<int> LearnedTactics { get; init; } = [];

    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Surname is null || CourtesyName is null || Biography is null ||
            FlagGlyph < -1 || BirthYear < 1 || AppearanceYear < BirthYear || LifespanTier is < 0 or > 7 ||
            Abilities.War < 1 || Abilities.Intelligence < 1 || Abilities.Politics < 1 || Abilities.Leadership < 1 ||
            Personality is < 0 or > 3 || Righteousness is < 0 or > 15 || Ambition is < 0 or > 15 || Affinity is < 0 or > 149 ||
            ParentId == Guid.Empty || ParentId == Id ||
            ScenarioParentId < 0 || ParentId is not null && ScenarioParentId is not null ||
            Proficiencies.IsDefault || Proficiencies.Length != 9 || Proficiencies.Any(p => p < 0) ||
            MasteredCategories.IsDefault || MasteredCategories.Any(c => c is < 0 or > 8) ||
            MasteredCategories.Distinct().Count() != MasteredCategories.Length || LearnedTactics is null || LearnedTactics.Any(t => t < 0))
            throw new InvalidDataException($"人物“{FullName}”的资料无效，请检查姓名、年份、属性及关系。");
        if (PortraitKey is not null && string.IsNullOrWhiteSpace(PortraitKey))
            throw new InvalidDataException("头像引用不能为空白。");
    }
}

public sealed record CreatedOfficerLibrary(int SchemaVersion, ImmutableArray<CreatedOfficer> Officers)
{
    public const int CurrentSchemaVersion = 3;
    public static CreatedOfficerLibrary Empty { get; } = new(CurrentSchemaVersion, []);
    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException("不支持的人物库版本。");
        if (Officers.IsDefault || Officers.Any(o => o is null) ||
            Officers.Select(o => o.Id).Distinct().Count() != Officers.Length)
            throw new InvalidDataException("人物身份重复，或缺少人物集合。");
        var people = Officers.ToDictionary(o => o.Id);
        foreach (var person in Officers)
        {
            person.Validate();
            var seen = new HashSet<Guid> { person.Id };
            for (var current = person; current.ParentId is Guid parent;)
            {
                if (!people.TryGetValue(parent, out current!)) throw new InvalidDataException("人物的亲代不在人物库中。");
                if (!seen.Add(parent)) throw new InvalidDataException("亲代关系形成循环。");
            }
        }
    }
    public static CreatedOfficerLibrary Decode(string json)
    {
        var result = JsonSerializer.Deserialize<CreatedOfficerLibrary>(json, PackageFiles.JsonOptions)
            ?? throw new InvalidDataException("人物库为空。");
        ContentStructure.Validate(result); result.Validate(); return result;
    }
    public string Encode() { Validate(); return JsonSerializer.Serialize(this, PackageFiles.JsonOptions); }
}

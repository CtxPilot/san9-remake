using San9.Content.Authoring;

namespace San9.Application.Authoring;

/// <summary>Owns an editor visit. Changes stay in memory until the host successfully saves the library.</summary>
public sealed class CreatedOfficerWorkspace(CreatedOfficerLibrary library)
{
    private readonly Dictionary<Guid, string> _surnameEdits = [];
    public CreatedOfficerLibrary Library { get; private set; } = library;
    public bool HasChanges { get; private set; }
    public OfficerFlagCatalog? FlagCatalog { get; private set; }
    public void UseFlagCatalog(OfficerFlagCatalog catalog) { catalog.Validate(); FlagCatalog = catalog; }
    public void Saved() => HasChanges = false;
    public CreatedOfficer Add()
    {
        var person = new CreatedOfficer(Guid.NewGuid(), "新武将");
        Set(person); return person;
    }
    public CreatedOfficer Duplicate(Guid id)
    {
        var original = Library.Officers.Single(p => p.Id == id);
        var newId = Guid.NewGuid();
        var person = original with { Id = newId, BloodlineRootId = original.BloodlineRootId == id ? newId : original.BloodlineRootId };
        Set(person); return person;
    }
    public void Set(CreatedOfficer person)
    {
        int index = -1;
        for (int i = 0; i < Library.Officers.Length; i++)
            if (Library.Officers[i].Id == person.Id) { index = i; break; }
        if (index >= 0 && Library.Officers[index] == person) return;
        if (index >= 0 && Library.Officers[index].Surname != person.Surname)
            _surnameEdits.TryAdd(person.Id, Library.Officers[index].Surname);
        Library = Library with { Officers = index < 0 ? Library.Officers.Add(person) : Library.Officers.SetItem(index, person) };
        HasChanges = true;
    }
    public bool AssignFlagFromSurname(Guid id)
    {
        var person = Library.Officers.SingleOrDefault(p => p.Id == id);
        if (person is null) return false;
        var catalog = FlagCatalog ?? throw new InvalidOperationException("旗字目录尚未载入。");
        int glyph = catalog.Resolve(person.Surname);
        Set(person with { FlagGlyph = glyph });
        _surnameEdits.Remove(id);
        return glyph != person.FlagGlyph;
    }
    public bool FinishSurnameEdit(Guid id, bool automatic)
    {
        if (!_surnameEdits.TryGetValue(id, out string? original)) return false;
        if (automatic && FlagCatalog is not null && Library.Officers.Any(p => p.Id == id && p.Surname != original))
            return AssignFlagFromSurname(id);
        _surnameEdits.Remove(id);
        return false;
    }
    public bool FinishSurnameEdits(bool automatic)
    {
        bool changed = false;
        foreach (var id in _surnameEdits.Keys.ToArray()) changed |= FinishSurnameEdit(id, automatic);
        return changed;
    }
    public void Delete(Guid id)
    {
        if (Library.Officers.Any(o => o.Id != id && (o.ParentId == id || o.BloodlineRootId == id))) throw new InvalidDataException("请先解除其他人物指向此人的亲代或血缘关系。");
        if (!Library.Officers.Any(o => o.Id == id)) return;
        Library = Library with { Officers = Library.Officers.RemoveAll(o => o.Id == id) };
        _surnameEdits.Remove(id);
        HasChanges = true;
    }
    public void Import(CreatedOfficerLibrary imported, bool createCopies)
    {
        imported.Validate();
        var ids = imported.Officers.ToDictionary(o => o.Id, o => createCopies ? Guid.NewGuid() : o.Id);
        var people = Library.Officers.ToBuilder();
        var positions = Library.Officers.Select((o, index) => (o.Id, index)).ToDictionary(p => p.Id, p => p.index);
        foreach (var person in imported.Officers)
        {
            var value = person with { Id = ids[person.Id], ParentId = person.ParentId is Guid parent ? ids[parent] : null,
                BloodlineRootId = person.BloodlineRootId is Guid root ? ids[root] : null };
            if (positions.TryGetValue(value.Id, out int index)) people[index] = value;
            else { positions.Add(value.Id, people.Count); people.Add(value); }
        }
        var candidate = Library with { Officers = people.ToImmutable() };
        candidate.Validate(); Library = candidate;
        foreach (var id in ids.Values) _surnameEdits.Remove(id);
        HasChanges = true;
    }
}

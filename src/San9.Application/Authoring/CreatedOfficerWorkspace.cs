using San9.Content.Authoring;

namespace San9.Application.Authoring;

/// <summary>Owns an editor visit. Changes stay in memory until the host successfully saves the library.</summary>
public sealed class CreatedOfficerWorkspace(CreatedOfficerLibrary library)
{
    public CreatedOfficerLibrary Library { get; private set; } = library;
    public bool HasChanges { get; private set; }
    public void Saved() => HasChanges = false;
    public CreatedOfficer Add()
    {
        var person = new CreatedOfficer(Guid.NewGuid(), "新武将");
        Set(person); return person;
    }
    public CreatedOfficer Duplicate(Guid id)
    {
        var person = Library.Officers.Single(p => p.Id == id) with { Id = Guid.NewGuid() };
        Set(person); return person;
    }
    public void Set(CreatedOfficer person)
    {
        int index = -1;
        for (int i = 0; i < Library.Officers.Length; i++)
            if (Library.Officers[i].Id == person.Id) { index = i; break; }
        if (index >= 0 && Library.Officers[index] == person) return;
        Library = Library with { Officers = index < 0 ? Library.Officers.Add(person) : Library.Officers.SetItem(index, person) };
        HasChanges = true;
    }
    public void Delete(Guid id)
    {
        if (Library.Officers.Any(o => o.ParentId == id)) throw new InvalidDataException("请先解除其他人物指向此人的亲代关系。");
        if (!Library.Officers.Any(o => o.Id == id)) return;
        Library = Library with { Officers = Library.Officers.RemoveAll(o => o.Id == id) };
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
            var value = person with { Id = ids[person.Id], ParentId = person.ParentId is Guid parent ? ids[parent] : null };
            if (positions.TryGetValue(value.Id, out int index)) people[index] = value;
            else { positions.Add(value.Id, people.Count); people.Add(value); }
        }
        var candidate = Library with { Officers = people.ToImmutable() };
        candidate.Validate(); Library = candidate; HasChanges = true;
    }
}

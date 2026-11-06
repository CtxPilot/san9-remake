using San9.Content.Packages;
using San9.Content.Scenarios;

namespace San9.Application.Authoring;

public sealed partial class SharedDataEditorWorkspace
{
    public OfficeDefinition GetOffice(int id) => Definitions.Information.Offices.Single(o => o.Id == id);
    public bool IsOfficeChanged(int id) => !ContentComparison.Equal(GetOffice(id), _saved.Information.Offices.SingleOrDefault(o => o.Id == id));

    public void SetOffice(OfficeDefinition office)
    {
        ContentStructure.Validate(office);
        var offices = Definitions.Information.Offices.ToArray();
        int index = Array.FindIndex(offices, o => o.Id == office.Id);
        if (index < 0) throw new InvalidDataException("只能修改现有官职，不能改变官职身份。");
        offices[index] = office;
        Definitions = Definitions with { Information = Definitions.Information with { Offices = offices } };
    }

    public IReadOnlyList<OfficeDefinition> OrderedOffices => OrderOffices(Offices).ToArray();

    public void RestoreOffice(int id)
    {
        var baseline = _officeBaselines[id];
        Definitions = Definitions with { Information = Definitions.Information with
        {
            Offices = Offices.Select(o => (o.Id == id ? baseline : o) with
                { GovernorPriority = _officeOrderBaseline[o.Id] }).ToArray()
        } };
    }

    public bool MoveOfficeTo(int id, int target)
    {
        var ordered = OrderedOffices.ToList();
        int index = ordered.FindIndex(o => o.Id == id);
        if (index < 0) throw new InvalidDataException("找不到官职。");
        if (target < 0 || target >= ordered.Count) throw new ArgumentOutOfRangeException(nameof(target));
        if (target == index) return false;
        var assigned = CatalogPriorityOrder.Move(ordered.Select(o => new CatalogPriorityOrder.Entry(o.Id, o.GovernorPriority)), id, target)
            .ToDictionary(e => e.Id, e => e.Priority);
        SetOfficeOrder(Offices.Select(o => o with { GovernorPriority = assigned[o.Id] }));
        return true;
    }

    private static IOrderedEnumerable<OfficeDefinition> OrderOffices(IEnumerable<OfficeDefinition> offices) =>
        offices.OrderByDescending(o => o.GovernorPriority).ThenBy(o => o.Id);

    private void SetOfficeOrder(IEnumerable<OfficeDefinition> ordered)
    {
        var entries = ordered.ToDictionary(o => o.Id);
        Definitions = Definitions with { Information = Definitions.Information with
            { Offices = Offices.Select(o => entries[o.Id]).ToArray() } };
    }

    public bool IsCreatedOffice(int id) => Offices.Any(o => o.Id == id) &&
        !_baseDefinitions.Information.Offices.Any(o => o.Id == id);

    public async Task<OfficeDefinition> CreateOfficeAsync(int? copyFrom = null, CancellationToken token = default)
    {
        var draft = Definitions;
        var source = copyFrom is int original ? GetOffice(original) : null;
        int id = await AllocateDefinitionIdentityAsync(DefinitionKind.Office, token);
        EnsureDefinitionDraft(draft, token);
        var office = source is not null ? source with { Id = id, Name = source.Name + "副本" } :
            new OfficeDefinition(id, "新官职", 10000, 0);
        var entries = InsertOffice(Offices, office, copyFrom).ToDictionary(o => o.Id);
        // Order is collection state. Extend its opening baseline independently of
        // draft reorders so restoring never leaves shifted neighbours behind.
        var orderBaseline = InsertOffice(Offices.Select(o => o with { GovernorPriority = _officeOrderBaseline[o.Id] }), office, copyFrom)
            .ToDictionary(o => o.Id, o => o.GovernorPriority);
        office = entries[id];
        Definitions = draft with
        {
            NextOfficeId = (long)id + 1,
            Information = draft.Information with { Offices = [.. draft.Information.Offices.Select(o => entries[o.Id]), office] }
        };
        _officeBaselines.Add(id, office);
        _officeOrderBaseline = orderBaseline;
        return office;
    }

    private static OfficeDefinition[] InsertOffice(IEnumerable<OfficeDefinition> offices, OfficeDefinition office, int? after)
    {
        var ordered = OrderOffices(offices).ToList();
        int position = after is int id ? ordered.FindIndex(o => o.Id == id) + 1 : ordered.Count;
        var priorities = CatalogPriorityOrder.Insert(ordered.Select(o => new CatalogPriorityOrder.Entry(o.Id, o.GovernorPriority)), office.Id, position)
            .ToDictionary(e => e.Id, e => e.Priority);
        return ordered.Append(office).Select(o => o with { GovernorPriority = priorities[o.Id] }).ToArray();
    }

    public async Task DeleteOfficeAsync(int id, CancellationToken token = default)
    {
        if (!IsCreatedOffice(id)) throw new InvalidDataException("仅能删除自建官职，原有官职可编辑、可复制。");
        var draft = Definitions;
        var candidate = draft with
        {
            Information = draft.Information with { Offices = draft.Information.Offices.Where(o => o.Id != id).ToArray() }
        };
        await ValidateDefinitionDeletionAsync(draft, candidate, "官职", token);
        _officeBaselines.Remove(id);
        _officeOrderBaseline.Remove(id);
    }
}

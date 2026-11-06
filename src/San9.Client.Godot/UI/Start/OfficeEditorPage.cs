using San9.Content.Scenarios;

namespace San9.Client.Godot;

public partial class OfficeEditorPage : SharedCatalogEditorPage
{
    protected override string Subject => "官职";
    protected override IEnumerable<(int Id, string Name)> Entries => Workspace.Offices.Select(o => (o.Id, o.Name));
    protected override bool Changed(int id) => Workspace.IsOfficeChanged(id);
    protected override void Restore(int id) => Workspace.RestoreOffice(id);
    private void Edit(int id, Func<OfficeDefinition, OfficeDefinition> change) => Workspace.SetOffice(change(Workspace.GetOffice(id)));
    protected override void BuildForm(int id)
    {
        var office = Workspace.GetOffice(id);
        TextField("名称", office.Name, v => Edit(id, p => p with { Name = v }));
        TextField("描述", office.Description, v => Edit(id, p => p with { Description = v }), true);
        var basics = Section("基本参数");
        Number(basics, "带兵量", office.CommandCapacity, 0, uint.MaxValue, v => Edit(id, p => p with { CommandCapacity = (uint)v }), "OfficeCapacity");
        Number(basics, "每旬俸禄", office.Salary, 0, int.MaxValue, v => Edit(id, p => p with { Salary = (int)v }), "OfficeSalary");
        Number(basics, "太守排序优先级", office.GovernorPriority, 0, int.MaxValue, v => Edit(id, p => p with { GovernorPriority = (int)v }), "OfficePriority");
        Choice(basics, "官职类别", ["武官", "文官"], (int)office.AppointmentGroup, v => Edit(id, p => p with { AppointmentGroup = (OfficeAppointmentGroup)v }));
        var bonuses = Section("任职能力加成");
        Number(bonuses, "统率", office.AbilityBonuses.Leadership, 0, int.MaxValue, v => Edit(id, p => p with { AbilityBonuses = p.AbilityBonuses with { Leadership = (int)v } }), "OfficeLeadership");
        Number(bonuses, "武力", office.AbilityBonuses.War, 0, int.MaxValue, v => Edit(id, p => p with { AbilityBonuses = p.AbilityBonuses with { War = (int)v } }));
        Number(bonuses, "智力", office.AbilityBonuses.Intelligence, 0, int.MaxValue, v => Edit(id, p => p with { AbilityBonuses = p.AbilityBonuses with { Intelligence = (int)v } }));
        Number(bonuses, "政治", office.AbilityBonuses.Politics, 0, int.MaxValue, v => Edit(id, p => p with { AbilityBonuses = p.AbilityBonuses with { Politics = (int)v } }));
        var requirements = Section("任命条件");
        var ranks = Workspace.Ranks.OrderBy(r => r.Precedence).ToArray();
        List<int?> rankIds = [null, .. ranks.Select(r => (int?)r.Id)];
        List<string> names = ["无爵位要求", .. ranks.Select(r => r.Name)];
        int selected = rankIds.IndexOf(office.RequiredRankId);
        if (selected < 0) { selected = rankIds.Count; rankIds.Add(office.RequiredRankId); names.Add($"缺失爵位 {office.RequiredRankId}"); }
        Choice(requirements, "所需爵位", names, selected, v => Edit(id, p => p with { RequiredRankId = rankIds[v] }));
        Toggle(Form, "优先于普通官职分配", office.FillBeforeOrdinaryOffices, v => Edit(id, p => p with { FillBeforeOrdinaryOffices = v }));
    }
}

using Godot;
using San9.Content.Scenarios;

namespace San9.Client.Godot;

public partial class RankEditorPage : SharedCatalogEditorPage
{
    private SharedCatalogActions _actions = null!;
    public override bool HasPendingChanges => _actions.Busy;
    public override bool CommitPending() => !_actions.Busy;
    protected override string RestoreText => "恢复爵位及顺序";
    protected override string RestoreTooltip => "恢复所选爵位的资料和整份爵位顺序，保留其他爵位的独立资料修改。";
    protected override void BuildSidebarActions(VBoxContainer sidebar)
    {
        _actions = new()
        {
            Page = this, Subject = "爵位", Identity = "Rank", Current = () => Current,
            CanDelete = Workspace.IsCreatedRank, EntryName = id => Workspace.GetRank(id).Name,
            Create = async (id, token) => (await Workspace.CreateRankAsync(id, token)).Id,
            Delete = Workspace.DeleteRankAsync, Select = SelectEntry
        };
        sidebar.AddChild(_actions);
    }
    protected override void RefreshActions() => _actions.Refresh();
    protected override ItemList CreateList() => new CatalogOrderList
    {
        CanReorder = () => !_actions.Busy && !IsFiltering && CanProcess(),
        CanDragItem = id => !Workspace.GetRank(id).IsUnranked,
        MaximumDropSlot = () => Workspace.Ranks.Count - 1,
        Reorder = (id, index) =>
        {
            if (Workspace.MoveRankTo(id, index)) { SelectEntry(id); Status("已调整爵位顺序及前两项身份，尚未保存。"); }
        },
        TooltipText = "拖动调整爵位顺序；前两项为皇帝、称帝前置，无爵位固定末尾。"
    };
    protected override string Subject => "爵位";
    protected override IEnumerable<(int Id, string Name)> Entries => Workspace.OrderedRanks.Select(r => (r.Id, r.Name));
    protected override Action<int, string> Rename => (id, value) => Edit(id, p => p with { Name = value });
    protected override bool Changed(int id) => Workspace.IsRankChanged(id);
    protected override void Restore(int id) => Workspace.RestoreRank(id);
    private void Edit(int id, Func<RankDefinition, RankDefinition> change) => Workspace.SetRank(change(Workspace.GetRank(id)));
    protected override void BuildForm(int id)
    {
        var rank = Workspace.GetRank(id);
        TextField("描述", rank.Description, v => Edit(id, p => p with { Description = v }), true, "RankDescription");
        var basics = Section("基本参数");
        Number(basics, "带兵量", rank.CommandCapacity, 0, uint.MaxValue, v => Edit(id, p => p with { CommandCapacity = (uint)v }), "RankCapacity");
        Number(basics, "所需信望", rank.RequiredTrust, 0, int.MaxValue, v => Edit(id, p => p with { RequiredTrust = (int)v }), "RankRequiredTrust");
        Number(basics, "忠诚加成", rank.LoyaltyBonus, 0, int.MaxValue, v => Edit(id, p => p with { LoyaltyBonus = (int)v }), "RankLoyaltyBonus");
        var flags = Options("爵位属性");
        Toggle(flags, "自动填补优先考虑四维总和", rank.PreferBalancedOfficeCandidates, v => Edit(id, p => p with { PreferBalancedOfficeCandidates = v }), "RankBalancedCandidates");
    }
}

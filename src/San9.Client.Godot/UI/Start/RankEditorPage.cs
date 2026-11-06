using San9.Content.Scenarios;

namespace San9.Client.Godot;

public partial class RankEditorPage : SharedCatalogEditorPage
{
    protected override string Subject => "爵位";
    protected override IEnumerable<(int Id, string Name)> Entries => Workspace.Ranks.Select(r => (r.Id, r.Name));
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
        Number(basics, "位阶", rank.Precedence, 0, int.MaxValue, v => Edit(id, p => p with { Precedence = (int)v }), "RankPrecedence", "数值越大，爵位越高；各爵位的位阶不能重复。");
        var flags = Options("爵位属性");
        Toggle(flags, "此项代表无爵位", rank.IsUnranked, v => Edit(id, p => p with { IsUnranked = v }), "RankIsUnranked", "只能有一项设为无爵位。");
        Toggle(flags, "优先考虑四维均衡的候选", rank.PreferBalancedOfficeCandidates, v => Edit(id, p => p with { PreferBalancedOfficeCandidates = v }), "RankBalancedCandidates");
    }
}

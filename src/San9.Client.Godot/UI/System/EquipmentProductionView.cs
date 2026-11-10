using Godot;
using San9.Application;
using San9.Application.Presentation;
using San9.Content.Text;
using San9.Core.Simulation;

namespace San9.Client.Godot;

public partial class EquipmentProductionView : ModalView, IModalInputRouter
{
    public GameSession Session { get; init; } = null!;
    public TextCatalog Texts { get; init; } = null!;
    public TextureScope Textures { get; init; } = null!;
    public TextureService Service { get; init; } = null!;
    public Action Closed { get; init; } = null!;
    public int SettlementId { get; init; }
    public bool Purchase { get; init; }
    private Control? _page;
    private static readonly CatalogPage[] ProductionPages = [new("制造", [
        new("name", "装备", 18, CatalogAlignment.Left), new("cost", "资金", 8, CatalogAlignment.Right),
        new("days", "日数", 8, CatalogAlignment.Right), new("state", "状态／条件", 28, CatalogAlignment.Left)])];
    private static readonly CatalogPage[] PurchasePages = [new("购买", [
        new("name", "装备", 18, CatalogAlignment.Left), new("cost", "资金", 8, CatalogAlignment.Right),
        new("days", "取得", 8, CatalogAlignment.Right), new("state", "状态／条件", 28, CatalogAlignment.Left)])];
    public override void _Ready() { Size = new(1024, 768); MouseFilter = MouseFilterEnum.Stop; ShowCatalog(); }
    private void Replace(Control page)
    {
        if (_page is not null) { RemoveChild(_page); _page.QueueFree(); }
        _page = page; AddChild(page);
    }
    private void ShowCatalog()
    {
        var recipes = (Purchase ? Session.PurchasableEquipment : Session.EquipmentRecipes).ToArray();
        var force = Session.Snapshot.Forces.FirstOrDefault(f => f.Id == Session.View.PlayerForceId);
        string State(int index)
        {
            var recipe = recipes[index];
            if (force is null) return "无可操作势力";
            if (Purchase) return recipe.PurchaseResearchProjectId is not string required || force.CompletedResearch.Contains(required)
                ? "可购买" : "需要 " + Session.ResearchProjectName(required);
            if (force.EquipmentProduction is { } active)
                return active.DefinitionId == recipe.Id ? $"制造中，剩余{active.DaysRemaining}日" : "其他装备制造中";
            return force.CompletedResearch.Contains(recipe.ResearchProjectId!) ? "可制造" : "需要 " + Session.ResearchProjectName(recipe.ResearchProjectId!);
        }
        var rows = recipes.Select((p, i) => new CatalogRow(i, [p.Name, Purchase ? p.PurchaseCost!.Value : p.ProductionCost, Purchase ? (CatalogCell)"即时" : p.ProductionDays, State(i)])).ToArray();
        var place = Session.View.Settlements.Single(s => s.Id == SettlementId).ResolveName(Texts);
        Replace(new CatalogView { Title = Purchase ? "购买装备" : "装备制造", HintText = $"执行设施　{place}。选择装备查看效果。", CloseTitle = "中止",
            Textures = Textures, Service = Service, Pages = Purchase ? PurchasePages : ProductionPages, ReadRows = _ => rows, Closed = Closed,
            Activated = index =>
            {
                var recipe = recipes[index]; string state = State(index); var bonus = recipe.BaseBonuses;
                string effect = $"品阶 {recipe.Grade}　要求 {OfficerEquipment.Requirements(recipe.MinimumAbilities)}\n统率+{bonus.Leadership} 武力+{bonus.War} 智力+{bonus.Intelligence} 政治+{bonus.Politics}";
                if (state != (Purchase ? "可购买" : "可制造")) { Notice($"{recipe.Name}　{state}\n{effect}"); return; }
                Confirm(Purchase ? $"购买 {recipe.Name}？\n资金 {recipe.PurchaseCost}，完成后进入库存。\n{effect}" :
                    $"制造 {recipe.Name}？\n资金 {recipe.ProductionCost}　{recipe.ProductionDays} 日。\n{effect}", () => Start(recipe.Id));
            } });
    }
    private void Start(string id)
    {
        if (Session.View.PlayerForceId is not int force) { Notice("当前没有可操作的势力。"); return; }
        GameCommand command = Purchase ? new PurchaseEquipmentCommand(Guid.NewGuid(), force, Session.Snapshot.Simulation.NextSequence, SettlementId, id) :
            new StartEquipmentProductionCommand(Guid.NewGuid(), force, Session.Snapshot.Simulation.NextSequence, SettlementId, id);
        var result = Session.Execute(new("player"), command);
        if (result.Submission.Accepted) { ShowCatalog(); return; }
        string action = Purchase ? "购买" : "制造";
        Notice(result.Submission.Rejection == CommandRejection.InsufficientFunds ? $"{action}失败，资金不足。" : $"{action}失败，请检查当前回合、据点和研究条件。");
    }
    private void Confirm(string message, Action yes)
    {
        var dialog = new GameDialog { Textures = Textures, Size = Size, PreferredWidth = 496 }; Replace(dialog);
        dialog.Confirmed += yes; dialog.Dismissed += ShowCatalog; dialog.Closed += ShowCatalog; dialog.SetMessage(message);
    }
    private void Notice(string message)
    {
        var dialog = new GameDialog { Textures = Textures, Size = Size }; Replace(dialog);
        dialog.Closed += ShowCatalog; dialog.SetNotice(message);
    }
}

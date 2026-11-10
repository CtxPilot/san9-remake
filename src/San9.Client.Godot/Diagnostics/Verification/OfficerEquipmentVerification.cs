using Godot;
using San9.Application;
using San9.Application.Presentation;
using San9.Content.Packages;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Exercises the real officer equipment controls using a prepared content package.</summary>
public partial class OfficerEquipmentVerification : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private async void Run()
    {
        try
        {
            string root = System.Environment.GetEnvironmentVariable("SAN9_EQUIPMENT_CONTENT")
                ?? throw new InvalidOperationException("Set SAN9_EQUIPMENT_CONTENT to a scenario package.");
            var package = await ClientContentFiles.LoadAsync(root);
            int selectedForce = package.Scenario.PlayerForceId ?? package.Scenario.Forces.First(f => !f.IsDormant && !f.IsBarbarian && f.LeaderId is not null).Id;
            var initial = GameSession.Start(package.Scenario, 42, playerForceIds: [selectedForce]);
            int player = initial.View.PlayerForceId ?? throw new InvalidOperationException("No player force.");
            var officer = initial.View.Officers.First(o => o.OwnerId == player && o.SettlementId is not null &&
                initial.Snapshot.Officers.Single(p => p.Id == o.Id).IsServing);
            var saved = initial.Capture();
            long itemId = saved.World.Equipment.Select(e => e.Id).DefaultIfEmpty(-1).Max() + 1;
            var session = GameSession.Restore(package.Scenario, saved with { World = saved.World with
            { Equipment = [.. saved.World.Equipment, new EquipmentInstance(itemId, "basic-spear", null, player, false)] } });
            var service = new TextureService(); service.Initialize(package, package.TextureRoot); AddChild(service);
            using var textures = service.CreateScope();
            textures.RequestPrefixes("detail-", "message-", "catalog-", "scroll-", "roster-");
            var state = new OfficerDetailState(); state.Open([officer.Id], officer.Id);
            var page = new OfficerDetailView { State = state, Session = session, Textures = textures, Texts = package.Text, Service = service,
                Read = id => OfficerDetails.Read(session.View, package.Text, id),
                ReadName = id => package.Text.Resolve(session.View.Officers.Single(o => o.Id == id).NameKey),
                OpenTreasures = _ => { }, CurrentChanged = _ => { }, Closed = () => { } };
            AddChild(page);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            page.GetNode<DetailActionButton>("EquipmentOpen").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var equipment = Descendants<OfficerEquipmentView>(page).Single();
            var labels = Descendants<Label>(equipment).Select(label => label.Text).ToHashSet();
            foreach (string slot in new[] { "主武器", "副武器", "头盔", "铠甲", "坐骑", "宝物" })
                if (!labels.Contains(slot)) throw new InvalidOperationException("Missing slot: " + slot);
            var choice = Descendants<OptionButton>(equipment).Single(node => node.Name == "EquipmentChoice_MainHand");
            int index = Enumerable.Range(0, choice.ItemCount).Single(i => choice.GetItemText(i).Contains("制式长枪", StringComparison.Ordinal));
            choice.Select(index); choice.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
            var apply = Descendants<Button>(equipment).Single(node => node.Name == "EquipmentApply_MainHand");
            if (apply.Disabled) throw new InvalidOperationException("Inventory equipment cannot be selected.");
            apply.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var equipped = session.Snapshot.Equipment.Single(e => e.Id == itemId);
            if (equipped.OfficerId != officer.Id || !equipped.Equipped) throw new InvalidOperationException("Equipment command did not apply.");
            GD.Print($"OFFICER_EQUIPMENT_OK officer={officer.Id} slots=6");
            page.Free(); service.Free(); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}

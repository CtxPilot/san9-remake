using Godot;
using San9.Content.Packages;

namespace San9.Client.Godot;

public partial class WorkflowLifecycleVerification
{
    private async Task CheckUnitGroupList(string packageRoot)
    {
        var package = await ClientContentFiles.LoadAsync(Path.GetFullPath(packageRoot));
        var scene = new Node(); AddChild(scene);
        var service = new TextureService(); service.Initialize(package, package.TextureRoot); scene.AddChild(service);
        using var textures = service.CreateScope();
        textures.RequestPrefixes("map-object-");
        var choices = new List<MapObjectChoice>
        {
            new(new(1, true), "刘备队", Colors.Green), new(new(2, true), "关羽队", Colors.Green),
            new(new(3, true), "张飞队", Colors.Green)
        };
        MapObjectList list = null!;
        list = new MapObjectList { Textures = textures, Position = new(200, 100), Removed = target =>
        {
            choices.RemoveAll(c => c.Target == target); list.SetChoices(choices); list.Fit(640, 320);
        } };
        scene.AddChild(list); list.SetChoices(choices); list.Fit(640, 320);
        await WaitForModal(() => service.ActiveReady, service);
        await Frame(); await Frame();
        void Inspect(string stage)
        {
            var scroll = list.GetChild<ScrollContainer>(0);
            var rows = Descendants<MapObjectRow>(list).ToArray();

            Check(rows.Length == choices.Count, "Selection change lost list rows.");
            Check(scroll.GetRect().End.X <= list.Size.X - 12, "List viewport escaped its frame.");
            Check(rows.All(r => r.Size.X <= scroll.Size.X && r.Size.Y == 20), $"A list row overflowed its viewport ({stage}).");
            Check(rows.All(r => !r.ToggleMode && !r.ShowSelection), "Persistent unit rows must not show selection indicators or latch pressed state.");
        }
        Inspect("BEFORE");
        var retained = Descendants<MapObjectRow>(list).Skip(1).ToArray();
        await Click(Descendants<MapObjectRow>(list).First().GetGlobalRect().GetCenter());
        list.Fit(640, 320); await Frame(); await Frame();
        Inspect("AFTER");
        Check(retained.SequenceEqual(Descendants<MapObjectRow>(list)), "Removing Liu Bei rebuilt unrelated rows.");
        list.Hide(); list.Show(); await Frame();
        Inspect("REOPEN");
        for (int i = 0; i < 12; i++)
        { list.SetChoices(choices); list.Fit(640, 320); await Frame(); await Frame(); }
        Inspect("REPEATED");
        if (DisplayServer.GetName() != "headless")
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = GetViewport().GetTexture().GetImage();
            image.SavePng(ProjectSettings.GlobalizePath("res://../../local/generated/verification/unit-group-list.png"));
        }
        var host = StrategyOverlayHost.Create(); scene.AddChild(host);
        var picker = new MapObjectPicker { Textures = textures }; host.AddChild(picker);
        IReadOnlyList<MapObjectTarget>? committed = null;
        var request = new MapObjectPickRequest(choices.Select(c => c.Target), _ => true, () => true,
            choices.Select(c => c.Target), ids => committed = ids);
        picker.Open(request, choices, new(400, 100));
        await WaitForModal(() => service.ActiveReady, service); await Frame(); await Frame();
        var first = Descendants<MapObjectRow>(picker).First();
        await Click(first.GetGlobalRect().GetCenter());
        Check(!first.Selected && !first.ToggleMode && committed is null, "Picker selection was committed or latched its row.");
        var confirm = Descendants<InformationActionButton>(picker).Single(button => button.Title == "决定");
        await Click(confirm.GetGlobalRect().GetCenter());
        Check(!picker.Visible && committed?.SequenceEqual([choices[1].Target]) == true, "Skinned confirmation lost temporary selection.");
        scene.Free();
        GD.Print("UNIT_GROUP_LIST_OK remove_bounds retained_rows selection_reopen multi_picker_confirm");
    }
}

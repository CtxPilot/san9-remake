using Godot;
using San9.Application;
using San9.Application.Presentation;
using San9.Client.Godot.Persistence;
using San9.Client.Godot.Settings;
using San9.Content.Packages;
using San9.Core.Domain;

namespace San9.Client.Godot;

public partial class WorkflowLifecycleVerification
{
    private async Task CheckUiPreferences(string packageRoot)
    {
        var package = await ClientContentFiles.LoadAsync(Path.GetFullPath(packageRoot));
        var scene = new Node(); AddChild(scene);
        var service = new TextureService(); service.Initialize(package, package.TextureRoot); scene.AddChild(service);
        using var textures = service.CreateScope();
        textures.RequestPrefixes("save-load-", "message-", "dialog-", "information-", "catalog-", "detail-", "editor-", "roster-", "scroll-", "text-icon-", "overview-");
        var host = StrategyOverlayHost.Create(); scene.AddChild(host);
        var session = GameSession.Start(package.Scenario, 1);
        int writes = 0;
        var preferences = new UiPreferencesSession(save: _ => writes++);
        var world = new MapWorld();
        try
        {
            var browser = new SaveLoadBrowser(); browser.Open(SaveLoadMode.Load);
            var slots = new SaveLoadView { State = browser, Preferences = preferences, Service = service, Textures = textures,
                Closed = browser.Close, Requested = _ => { } };
            var page = OverlayPage.Create(slots, Colors.Black); host.AddChild(page);
            await WaitForModal(() => service.ActiveReady, service); await Frame();
            var tab = slots.GetChildren().OfType<SaveLoadButton>().Single(button => button.Title == "11-15");
            await Click(tab.GetGlobalRect().GetCenter());
            Check(preferences.Current.SaveLoadPage == 2, "Save/load page click did not update shared preferences.");
            page.Free();
            var otherBrowser = new SaveLoadBrowser(); otherBrowser.Open(SaveLoadMode.Save, preferences.Current.SaveLoadPage);
            Check(otherBrowser.Page == 2 && writes == 0, "Cancelled browsing lost its page or wrote preferences before shutdown.");

            preferences.SetMap(SelectionViewGroup.Cities, false);
            var information = new InformationPanel { Session = session, Preferences = preferences, Texts = package.Text,
                Service = service, Skin = package.Skin, World = world, RecordsLog = new(), Chronicle = package.Presentation.Chronicle! };
            host.AddChild(information);
            information.Open(InformationCategory.Cities);
            await WaitForModal(() => service.ActiveReady, service); await Frame();
            Check(!information.State.IsMap, "City information ignored its preferred list mode.");
            Descendants<CatalogView>(information).Single().ShowMap!();
            Check(information.State.IsMap && preferences.IsMap(SelectionViewGroup.Cities), "Information map toggle did not update its shared group.");
            information.Back(); information.Open(InformationCategory.Cities);
            Check(information.State.IsMap, "Reopening information forgot map mode.");
            information.Free();

            var draft = new WorldEditorDraft(session, WorldEditorKind.Cities, package.Text);
            var editor = new WorldEditorView { Draft = draft, Preferences = preferences, Texts = package.Text,
                Service = service, Textures = textures, Skin = package.Skin, MapPreview = (_, _) => new Control(),
                Closed = () => { }, Apply = operation => session.Apply(session.Request(operation)) };
            host.AddChild(editor);
            await WaitForModal(() => service.ActiveReady, service); await Frame();
            Check(!editor.IsCatalog && draft.Catalog.Rows.Length > 0, "Editor did not share city map mode or initialize detail candidates.");
            Descendants<InformationMapView>(editor).Single().ShowList();
            Check(editor.IsCatalog && !preferences.IsMap(SelectionViewGroup.Cities), "Editor list toggle did not update information's city preference.");
            Check(preferences.IsMap(SelectionViewGroup.Forces) && preferences.IsMap(SelectionViewGroup.Facilities), "City switching leaked into other selection groups.");
            editor.Free();
            Check(writes == 0, "UI navigation wrote preferences before application shutdown.");
            preferences.Save();
            Check(writes == 1, "Application shutdown did not save one combined preference snapshot.");
            GD.Print("UI_PREFERENCES_OK slots=mouse-page,cancel,shared information=reopen editor=shared-mode,initial-candidates storage=shutdown-only");
        }
        finally { scene.Free(); world.Free(); }
    }
}

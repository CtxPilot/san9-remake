using System.Text.Json;
using San9.Content.Images;
using San9.Content.Interface;
using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class ScenarioSelectionPresentationTests
{
    [Fact]
    public void PreviewReferencesMustResolveWithinItsCatalogAndMap()
    {
        var sprite = new StartMenuSprite("image", 0, 0);
        var button = sprite with { Columns = 4 };
        ImageAsset[] images = [new("image", "image.png", 160, 40)];
        var entry = new ScenarioPreviewEntry("first", "history", 184, 2, "黄巾", "简介", new() { [7] = "green" });
        var map = new OpeningMapDefinition(16, 16, "image", "image", new(sprite, 7),
            new() { [7] = new(new(4, 8), [new(0, 0, 16)]) }, new() { ["green"] = new("image", null, "00ff00") }, "green");
        var scene = new ScenarioSelectionPresentation("选择", "提示", "返回", [sprite], map, sprite,
            sprite, "image", sprite, button, [button],
            [new("history", "一般", "提示", new(0, 0))], [entry])
        { Forces = new([sprite], sprite, sprite, button, button, button)
            { JoinForcePrompt = "使其所属于势力吗？", MapSkin = ForceSelectionPresentation.RequiredSkinRoles.ToDictionary(role => role, _ => images[0]),
                CreateForces = button, DeployOfficers = button, CancelDeployment = button,
                NewForces = new([sprite], Enumerable.Range(0, 8).Select(_ => new NewForceSlotPresentation(button, button, button, sprite,
                    new(0, 0), new(0, 0), new(0, 0), new(0, 0))).ToArray(), button, button) },
            Preview = new(sprite, sprite, 1, 1, 0, 0),
            Settings = new([sprite], [sprite, sprite], "image", new(["image"], 1, 0, 0, .25), button, button, button, sprite) };
        scene.Validate(images);
        var menu = new StartMenuPresentation([], [], [], button, button, "提示", []) { QuitPrompt = "退出？" };
        var opening = new OpeningContent(menu, scene) { Audio = new("music", "start", []) };
        string json = JsonSerializer.Serialize(opening, PackageFiles.JsonOptions);
        using (var document = JsonDocument.Parse(json))
        {
            Assert.Equal(OpeningContent.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Single(document.RootElement.GetProperty("files").EnumerateArray());
        }
        var restored = JsonSerializer.Deserialize<OpeningContent>(json, PackageFiles.JsonOptions)!;
        restored.Selection.Validate(images);
        Assert.All(restored.Selection.Forces.MapSkin.Values, image => Assert.Same(restored.Selection.Forces.MapSkin.Values.First().File, image.File));
        foreach (var invalid in new[]
        {
            scene with { Entries = [entry with { CityColors = new() { [8] = "green" } }] },
            scene with { Entries = [entry with { CityColors = new() { [7] = "unknown" } }] },
            scene with { Entries = [entry with { CategoryId = "unknown" }] },
            scene with { Entries = [entry, entry] },
            scene with { Map = map with { Colors = new() { ["green"] = new("missing", null, "00ff00") } } },
            scene with { Entries = [entry with { Month = 13 }] },
            scene with { Forces = scene.Forces with { Choose = button with { Columns = 0 } } },
            scene with { Forces = scene.Forces with { MapSkin = [] } },
            scene with { Forces = scene.Forces with { JoinForcePrompt = "" } },
            scene with { Forces = scene.Forces with { NewForces = scene.Forces.NewForces with { Slots = [] } } },
            scene with { Forces = scene.Forces with { MapSkin = new(scene.Forces.MapSkin) { ["opening-panel"] = new("other", "missing.png", 160, 40) } } },
            scene with { Settings = scene.Settings with { ChoiceHighlight = "missing" } },
            scene with { Settings = scene.Settings with { Illustration = scene.Settings.Illustration with { Columns = 3 } } }
        })
            Assert.Throws<InvalidDataException>(() => invalid.Validate(images));
    }
}

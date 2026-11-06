using System.Text.Json;
using San9.Application.Authoring;
using San9.Content.Images;
using San9.Content.Packages;

namespace San9.Application.Tests;

public sealed class PortraitAnchorUpgradeTests
{
    [Fact]
    public async Task ExplicitUpgradePreservesImagesKeysAndBothPreviouslyRenderedRegions()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-portrait-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "portraits.json");
            await File.WriteAllTextAsync(path, """
                {"schemaVersion":1,"id":"my-portraits","portraits":[
                  {"id":"first","name":"原有头像","image":{"path":"first.png","width":512,"height":512,"channels":3},"anchorX":0.1,"anchorY":0.05}],
                 "overrides":[{"target":"portrait/1","anchorX":0.2,"anchorY":0.1}]}
                """);
            byte[] image = new PngImage(512, 512, 3, new byte[512 * 512 * 3]).Encode();
            await File.WriteAllBytesAsync(Path.Combine(root, "first.png"), image);
            await Assert.ThrowsAnyAsync<JsonException>(() => PortraitPackFiles.LoadAsync(root));
            PortraitAnchorUpgrade.UserPack(root);
            var pack = await PortraitPackFiles.LoadAsync(root);
            Assert.Equal(PortraitPack.CurrentSchemaVersion, pack.SchemaVersion);
            Assert.Equal("user/my-portraits/first", pack.Key(pack.Portraits[0].Id));
            Assert.Equal(new PortraitAnchor(.1, .05), pack.Portraits[0].BustAnchor);
            Assert.Equal(PortraitGeometry.FaceFromBust(new(.1, .05)), pack.Portraits[0].FaceAnchor);
            Assert.Equal(PortraitGeometry.FaceFromBust(new(.2, .1)), pack.Overrides[0].FaceAnchor);
            Assert.Equal(image, await File.ReadAllBytesAsync(Path.Combine(root, "first.png")));
            byte[] current = await File.ReadAllBytesAsync(path);
            Assert.Throws<InvalidDataException>(() => PortraitAnchorUpgrade.UserPack(root));
            Assert.Equal(current, await File.ReadAllBytesAsync(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void InvalidLegacyAnchorDoesNotOverwriteOriginalManifest()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-portrait-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "portraits.json");
            File.WriteAllText(path, """{"schemaVersion":1,"id":"my-portraits","portraits":[],"overrides":[{"target":"portrait/1","anchorX":0.9,"anchorY":0}]}""");
            byte[] original = File.ReadAllBytes(path);
            Assert.Throws<InvalidDataException>(() => PortraitAnchorUpgrade.UserPack(root));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}

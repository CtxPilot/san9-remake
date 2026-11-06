using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Content.Images;
using San9.Content.Interface;
using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class ImageFileTableTests
{
    private static TexturePackage Package()
    {
        var file = new ImageFile("images/shared.png", 512, 256, 4);
        var first = new ImageAsset("first", file, 2, 2, 64, 80);
        var second = new ImageAsset("second", file, 72, 2, 64, 80);
        var separate = first with { Id = "separate", File = file with { Path = "images/separate.png" } };
        return new(TexturePackage.CurrentSchemaVersion, 65, "test", [],
            new([], [first, second], [], [], [], new int[288], new int[16], [], new byte[32]),
            new(InterfaceSkin.CurrentSchemaVersion, [separate], [], [], []),
            new(3, [new("alias", "interface", [new(0, 0, first)])]),
            [new(0, new("portrait/0", "images/portraits/0.png", 240, 240, 3), new(0, 0), new(32d / 240, 30d / 240)),
             new(1, new("portrait/1", "images/portraits/1.png", 512, 512, 4), new(0, 0), new(32d / 240, 30d / 240))]);
    }

    private static JsonObject Document(TexturePackage? package = null) =>
        JsonSerializer.SerializeToNode(package ?? Package(), PackageFiles.JsonOptions)!.AsObject();

    [Fact]
    public void FileTableSharesPhysicalMetadataAcrossCategoriesAndPreservesRegionsAndSeparateFiles()
    {
        var package = Package();
        var document = Document(package);
        Assert.Equal(4, document["files"]!.AsArray().Count);
        int first = document["map"]!["assets"]![0]!["file"]!.GetValue<int>();
        Assert.Equal(first, document["map"]!["assets"]![1]!["file"]!.GetValue<int>());
        Assert.Equal(first, document["library"]!["archives"]![0]!["variants"]![0]!["image"]!["file"]!.GetValue<int>());
        Assert.NotEqual(first, document["skin"]!["assets"]![0]!["file"]!.GetValue<int>());
        var restored = document.Deserialize<TexturePackage>(PackageFiles.JsonOptions)!;
        Assert.Equal(package.Map.Assets, restored.Map.Assets);
        Assert.Equal(package.Portraits, restored.Portraits);
        Assert.Equal(package.Skin.Assets, restored.Skin.Assets);
        Assert.Same(restored.Map.Assets[0].File, restored.Map.Assets[1].File);
        Assert.Same(restored.Map.Assets[0].File, restored.Library.Assets.Single().File);
        Assert.NotSame(restored.Map.Assets[0].File, restored.Skin.Assets[0].File);
        Assert.True(JsonNode.DeepEquals(document, Document(restored)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void UnknownFileIndicesAreRejected(int index)
    {
        var document = Document();
        document["map"]!["assets"]![0]!["file"] = index;
        Assert.Throws<InvalidDataException>(() => document.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
    }

    [Fact]
    public void InvalidCatalogsAndInlineReferencesAreRejected()
    {
        var missing = Document(); missing.Remove("files");
        Assert.Throws<JsonException>(() => missing.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
        var duplicate = Document(); duplicate["files"]!.AsArray().Add(duplicate["files"]![0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => duplicate.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
        var incomplete = Document(); incomplete["files"]![0]!.AsObject().Remove("width");
        Assert.Throws<JsonException>(() => incomplete.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
        var nullFile = Document(); nullFile["files"]![0] = null;
        Assert.Throws<InvalidDataException>(() => nullFile.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
        var escape = Document(); escape["files"]![0]!["path"] = "../outside.png";
        Assert.Throws<InvalidDataException>(() => escape.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
        var inline = Document();
        inline["map"]!["assets"]![0]!["file"] = JsonSerializer.SerializeToNode(Package().Map.Assets[0].File, PackageFiles.JsonOptions);
        Assert.Throws<JsonException>(() => inline.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
        var region = Document(); region["map"]!["assets"]![0]!["width"] = 1024;
        Assert.Throws<InvalidDataException>(() => region.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
        var old = Document(); old["schemaVersion"] = TexturePackage.CurrentSchemaVersion - 1;
        Assert.Throws<InvalidDataException>(() => old.Deserialize<TexturePackage>(PackageFiles.JsonOptions));
    }

    [Fact]
    public void OnePathCannotDeclareConflictingPhysicalMetadata()
    {
        var package = Package();
        var image = package.Map.Assets[0];
        var conflicting = package with { Map = package.Map with { Assets = [image, image with { File = image.File with { Channels = 3 } }] } };
        Assert.Throws<InvalidDataException>(() => Document(conflicting));
    }

    [Fact]
    public async Task ConcurrentDocumentsResolveOnlyTheirOwnFileTables()
    {
        var first = Package();
        var second = first with { Portraits = [new(9, new("portrait/9", "other.png", 1024, 1024, 4), new(0, 0), new(32d / 240, 30d / 240))] };
        string[] documents = [Document(first).ToJsonString(), Document(second).ToJsonString()];
        var results = await Task.WhenAll(documents.Select(json => Task.Run(() => JsonSerializer.Deserialize<TexturePackage>(json, PackageFiles.JsonOptions)!)));
        Assert.Equal(first.Portraits, results[0].Portraits);
        Assert.Equal(second.Portraits, results[1].Portraits);
        Assert.NotSame(results[0].Map.Assets[0].File, results[1].Map.Assets[0].File);
    }
}

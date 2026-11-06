using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Application.Persistence;
using San9.Content.Images;
using San9.Content.Packages;

namespace San9.Application.Authoring;

/// <summary>Explicit offline metadata upgrade. Runtime readers only accept the current contracts.</summary>
public static class PortraitAnchorUpgrade
{
    public static void TexturePackage(string root) => Upgrade(Path.Combine(root, "textures.json"), 6, 7, texture: true);
    public static void UserPack(string root) => Upgrade(Path.Combine(root, "portraits.json"), 1, 2, texture: false);

    private static void Upgrade(string path, int sourceVersion, int targetVersion, bool texture)
    {
        byte[] original = File.ReadAllBytes(path);
        var document = JsonNode.Parse(original)?.AsObject() ?? throw new InvalidDataException("Empty portrait metadata.");
        if (document["schemaVersion"]?.GetValue<int>() != sourceVersion)
            throw new InvalidDataException($"Expected schema {sourceVersion}; this command only upgrades the previous portrait anchor format.");
        foreach (var entry in document["portraits"]?.AsArray() ?? throw new InvalidDataException("Missing portraits.")) Split(entry);
        if (!texture)
            foreach (var entry in document["overrides"]?.AsArray() ?? throw new InvalidDataException("Missing overrides.")) Split(entry);
        document["schemaVersion"] = targetVersion;
        if (texture)
        {
            var package = document.Deserialize<TexturePackage>(PackageFiles.JsonOptions)!;
            foreach (var portrait in package.Portraits) portrait.Validate();
        }
        else document.Deserialize<PortraitPack>(PackageFiles.JsonOptions)!.Validate();
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(document, PackageFiles.JsonOptions), () =>
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(original))
                throw new IOException("Portrait metadata changed during upgrade.");
        });
    }

    private static void Split(JsonNode? entry)
    {
        var value = entry?.AsObject() ?? throw new InvalidDataException("Missing portrait.");
        var bust = new PortraitAnchor(value["anchorX"]?.GetValue<double>() ?? throw new InvalidDataException("Missing anchorX."),
            value["anchorY"]?.GetValue<double>() ?? throw new InvalidDataException("Missing anchorY."));
        bust.Validate(PortraitView.Bust);
        if (value.ContainsKey("bustAnchor") || value.ContainsKey("faceAnchor"))
            throw new InvalidDataException("Mixed portrait anchor formats.");
        value.Remove("anchorX"); value.Remove("anchorY");
        value["bustAnchor"] = JsonSerializer.SerializeToNode(bust, PackageFiles.JsonOptions);
        value["faceAnchor"] = JsonSerializer.SerializeToNode(PortraitGeometry.FaceFromBust(bust), PackageFiles.JsonOptions);
    }
}

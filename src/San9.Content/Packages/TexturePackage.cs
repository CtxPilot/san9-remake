using System.Text.Json;
using System.Text.Json.Nodes;
using San9.Content.Images;
using San9.Content.Interface;
using San9.Content.Maps;

namespace San9.Content.Packages;

public sealed record TextureSourceStamp(string Name, long Length, DateTime LastWriteTimeUtc);

public sealed record CloudPlacement(int Side, int A, int B, int C);

public sealed record MapResources(MapSeason[] Seasons, ImageAsset[] Assets, MapMask[] Masks,
    MapComposition[] Compositions, MapAnimationClip[] Clips, int[] MaskPositions,
    int[] MaskHeightAdjustments, CloudPlacement[] CloudPlacements, byte[] MinimapColors)
{
    [System.Text.Json.Serialization.JsonRequired] public MapUnitCatalog Units { get; init; } = MapUnitCatalog.Empty;
}

/// <summary>Source-version resources. No scenario state or scenario identifier belongs here.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(TexturePackageJsonConverter))]
public sealed record TexturePackage(int SchemaVersion, int ConversionVersion, string SourceBuild, TextureSourceStamp[] Sources,
    MapResources Map, InterfaceSkin Skin, TextureLibrary Library, PortraitDefinition[] Portraits)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, string> PortraitImageSources { get; init; } = new Dictionary<string, string>();
    public const int CurrentSchemaVersion = 7;
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<ImageAsset> Assets => Map.Assets.Concat(Map.Seasons.SelectMany(s => s.Textures))
        .Concat(Skin.Assets).Concat(Library.Assets).Concat(Portraits.Select(p => p.Image));

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || Map.MinimapColors.Length != 32 || Map.MaskPositions.Length != 288 ||
            Map.MaskHeightAdjustments.Length != 16) throw new InvalidDataException("Invalid texture package.");
        if (Portraits.Select(p => p.Id).Distinct().Count() != Portraits.Length) throw new InvalidDataException("Duplicate portrait ID.");
        foreach (var portrait in Portraits) portrait.Validate();
        Map.Units.Validate(Map.Clips.ToDictionary(c => c.Id), Map.Assets.Select(a => a.Id).ToHashSet());
        MapPackageFiles.ValidateBuildingSmoke(Map.Compositions, Map.Clips.ToDictionary(c => c.Id));
        Skin.Validate(); Library.Validate(); ImageAssetFiles.ValidateCatalog(Assets);
        MapPackageFiles.ValidateAnimations(new(Map.Clips, []), Map.Assets.ToDictionary(a => a.Id), new HashSet<int>());
    }
}

public static class TexturePackageFiles
{
    private static ImageFileTable ReadFiles(JsonObject manifest)
    {
        var files = manifest["files"]?.Deserialize<ImageFile[]>(PackageFiles.JsonOptions)
            ?? throw new JsonException("Missing image file table.");
        ContentStructure.Validate(files);
        return new(files);
    }

    public static T ReadUnit<T>(JsonObject manifest, string unit)
    {
        return manifest[unit].Deserialize<T>(ReadFiles(manifest).Options(PackageFiles.JsonOptions))
            ?? throw new InvalidDataException($"Missing texture unit: {unit}");
    }

    /// <summary>Replaces a unit without interpreting its obsolete definitions. Retained file indices stay valid.</summary>
    public static TexturePackage ReplaceUnit<T>(JsonObject manifest, string unit, T replacement)
    {
        var table = ReadFiles(manifest);
        manifest[unit] = JsonSerializer.SerializeToNode(replacement, table.Options(PackageFiles.JsonOptions));
        manifest["files"] = JsonSerializer.SerializeToNode(table.Files, PackageFiles.JsonOptions);
        return manifest.Deserialize<TexturePackage>(PackageFiles.JsonOptions)
            ?? throw new InvalidDataException("Empty texture package.");
    }

    public static async Task<TexturePackage> LoadAsync(string root, CancellationToken token = default, string? portraitPackRoot = null)
    {
        var package = await PackageFiles.ReadAsync<TexturePackage>(Path.Combine(root, "textures.json"), token);
        package.Validate();
        if (portraitPackRoot is not null)
        {
            var pack = await PortraitPackFiles.LoadAsync(portraitPackRoot, token);
            var resolved = pack.Resolve(package.Portraits, portraitPackRoot);
            package = package with { Portraits = resolved.Portraits, PortraitImageSources = resolved.ImageSources };
            package.Validate();
        }
        return package;
    }
}

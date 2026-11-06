using San9.Content.Packages;

namespace San9.Content.Images;

public sealed record AuthoredPortrait(string Id, string Name, ImageFile Image, PortraitAnchor BustAnchor, PortraitAnchor FaceAnchor);
public sealed record PortraitOverride(string Target, PortraitAnchor BustAnchor, PortraitAnchor FaceAnchor)
{
    public ImageFile? Image { get; init; }
}

/// <summary>User-owned images and explicit crop overrides; independent of source-version packages.</summary>
public sealed record PortraitPack(int SchemaVersion, string Id, AuthoredPortrait[] Portraits, PortraitOverride[] Overrides)
{
    public const int CurrentSchemaVersion = 2;
    public static PortraitPack Empty => new(CurrentSchemaVersion, "my-portraits", [], []);
    public string Key(string id) => $"user/{Id}/{id}";

    public IEnumerable<ImageFile> ImageFiles() => Portraits.Select(p => p.Image)
        .Concat(Overrides.Where(p => p.Image is not null).Select(p => p.Image!));

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || !ValidId(Id) || Portraits.Select(p => p.Id).Distinct().Count() != Portraits.Length ||
            Overrides.Select(p => p.Target).Distinct().Count() != Overrides.Length)
            throw new InvalidDataException("Invalid portrait pack version or duplicate identity.");
        foreach (var portrait in Portraits)
        {
            if (!ValidId(portrait.Id) || string.IsNullOrWhiteSpace(portrait.Name) || portrait.Image.Channels != 3)
                throw new InvalidDataException("Invalid authored portrait.");
            _ = PackageFiles.Resolve(".", portrait.Image.Path);
            new PortraitDefinition(0, new(Key(portrait.Id), portrait.Image, 0, 0, portrait.Image.Width, portrait.Image.Height),
                portrait.BustAnchor, portrait.FaceAnchor).Validate();
        }
        foreach (var entry in Overrides)
        {
            if (string.IsNullOrWhiteSpace(entry.Target)) throw new InvalidDataException("Missing portrait override target.");
            var image = entry.Image ?? new ImageFile("check.png", 240, 240, 3);
            if (image.Channels != 3) throw new InvalidDataException("Invalid replacement portrait image.");
            _ = PackageFiles.Resolve(".", image.Path);
            new PortraitDefinition(0, new(entry.Target, image, 0, 0, image.Width, image.Height), entry.BustAnchor, entry.FaceAnchor).Validate();
        }
    }

    private static bool ValidId(string value) => !string.IsNullOrEmpty(value) &&
        value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');

    public PortraitCatalog Resolve(IEnumerable<PortraitDefinition> basis, string root)
    {
        Validate();
        var entries = basis.ToDictionary(p => p.Image.Id);
        var baseKeys = entries.Keys.ToHashSet();
        var sources = new Dictionary<string, string>();
        // The numeric field is a runtime display ordinal. Persisted user identity is the namespaced key.
        int ordinal = entries.Values.Select(p => p.Id).DefaultIfEmpty(-1).Max();
        foreach (var entry in Portraits)
        {
            string path = $"portrait-packs/{Id}/{entry.Image.Path.Replace('\\', '/')}";
            var file = entry.Image with { Path = path };
            var image = new ImageAsset(Key(entry.Id), file, 0, 0, file.Width, file.Height);
            if (!entries.TryAdd(image.Id, new(checked(++ordinal), image, entry.BustAnchor, entry.FaceAnchor) { DisplayName = entry.Name }))
                throw new InvalidDataException($"Duplicate portrait: {image.Id}");
            sources[path] = PackageFiles.Resolve(root, entry.Image.Path);
        }
        foreach (var entry in Overrides)
        {
            if (!baseKeys.Contains(entry.Target) || !entries.TryGetValue(entry.Target, out var portrait))
                throw new InvalidDataException($"Invalid base portrait override: {entry.Target}");
            if (entry.Image is { } replacement)
            {
                string path = $"portrait-packs/{Id}/{replacement.Path.Replace('\\', '/')}";
                var file = replacement with { Path = path };
                portrait = portrait with { Image = new(entry.Target, file, 0, 0, file.Width, file.Height) };
                sources[path] = PackageFiles.Resolve(root, replacement.Path);
            }
            entries[entry.Target] = portrait with { BustAnchor = entry.BustAnchor, FaceAnchor = entry.FaceAnchor };
        }
        return new(entries.Values.ToArray(), sources);
    }
}

public sealed record PortraitCatalog(PortraitDefinition[] Portraits, IReadOnlyDictionary<string, string> ImageSources);

public static class PortraitPackFiles
{
    public static async Task<PortraitPack> LoadAsync(string root, CancellationToken token = default)
    {
        string path = Path.Combine(root, "portraits.json");
        if (!File.Exists(path)) return PortraitPack.Empty;
        var pack = await PackageFiles.ReadAsync<PortraitPack>(path, token);
        pack.Validate();
        foreach (var image in pack.ImageFiles())
            if (!File.Exists(PackageFiles.Resolve(root, image.Path)))
                throw new InvalidDataException($"Missing portrait image: {image.Path}");
        return pack;
    }
}

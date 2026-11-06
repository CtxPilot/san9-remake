using System.Text.Json;
using San9.Application.Persistence;
using San9.Content.Images;
using San9.Content.Packages;

namespace San9.Application.Authoring;

/// <summary>Owns the complete user pack draft, history and manifest publication.</summary>
public sealed class PortraitEditorWorkspace : IDisposable
{
    private readonly PortraitDefinition[] _basis;
    private readonly IReadOnlyDictionary<string, PortraitDefinition> _baseByKey;
    private Dictionary<string, PortraitDefinition> _savedPortraits;
    private PortraitPack _resolvedPack;
    private PortraitCatalog _catalog;
    private Dictionary<string, PortraitDefinition> _portraits;
    private readonly PortraitPack _opened;
    private PortraitPack _saved;
    private byte[]? _expected;
    private readonly Dictionary<string, byte[]> _images = [];
    private readonly HashSet<string> _ownedImages = [];
    private readonly Dictionary<string, AuthoredPortrait> _created = [];
    private readonly Stack<PortraitPack> _undo = [], _redo = [];
    public string Root { get; }
    public string ManifestPath => Path.Combine(Root, "portraits.json");
    public PortraitPack Pack { get; private set; }
    public bool HasChanges => !Equal(Pack, _saved);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public PortraitCatalog Catalog
    {
        get
        {
            if (!ReferenceEquals(_resolvedPack, Pack))
            {
                _catalog = Pack.Resolve(_basis, Root);
                _portraits = _catalog.Portraits.ToDictionary(p => p.Image.Id);
                _resolvedPack = Pack;
            }
            return _catalog;
        }
    }
    public PortraitDefinition Portrait(string key) { _ = Catalog; return _portraits[key]; }

    private PortraitEditorWorkspace(string root, PortraitDefinition[] basis, PortraitPack pack, byte[]? expected)
    {
        Root = root; _basis = basis.ToArray(); _baseByKey = _basis.ToDictionary(p => p.Image.Id);
        Pack = _opened = _saved = _resolvedPack = pack; _expected = expected;
        _catalog = pack.Resolve(_basis, root); _portraits = _catalog.Portraits.ToDictionary(p => p.Image.Id);
        _savedPortraits = _portraits;
        _ownedImages.UnionWith(pack.ImageFiles().Select(p => p.Path));
    }

    public static async Task<PortraitEditorWorkspace> OpenAsync(string root, PortraitDefinition[] basis, CancellationToken token = default)
    {
        root = Path.GetFullPath(root);
        string path = Path.Combine(root, "portraits.json");
        byte[]? bytes = File.Exists(path) ? await File.ReadAllBytesAsync(path, token) : null;
        var pack = bytes is null ? PortraitPack.Empty : JsonSerializer.Deserialize<PortraitPack>(bytes, PackageFiles.JsonOptions)
            ?? throw new InvalidDataException("Empty portrait pack.");
        ContentStructure.Validate(pack);
        foreach (var image in pack.ImageFiles())
            if (!File.Exists(PackageFiles.Resolve(root, image.Path)))
                throw new InvalidDataException($"Missing portrait image: {image.Path}");
        return new(root, basis, pack, bytes);
    }

    public string Add(string name, PngImage image)
    {
        ValidateImage(image);
        string id = Guid.NewGuid().ToString("N"), path = $"images/{id}.png";
        var entry = new AuthoredPortrait(id, name.Trim(), new(path, image.Width, image.Height, 3),
            PortraitGeometry.DefaultBust.SnapToPixels(PortraitView.Bust, image.Width, image.Height),
            PortraitGeometry.FaceFromBust(PortraitGeometry.DefaultBust).SnapToPixels(PortraitView.Face, image.Width, image.Height));
        var next = Pack with { Portraits = [.. Pack.Portraits, entry] };
        byte[] bytes = image.Encode();
        Change(next); _images[path] = bytes; _ownedImages.Add(path); _created[id] = entry;
        return Pack.Key(id);
    }

    public byte[]? PendingImage(string key)
    {
        var file = Pack.Portraits.SingleOrDefault(p => Pack.Key(p.Id) == key)?.Image
            ?? Pack.Overrides.SingleOrDefault(p => p.Target == key)?.Image;
        return file is not null && _images.TryGetValue(file.Path, out var bytes) ? bytes : null;
    }

    public string Name(string key)
    {
        var portrait = Portrait(key);
        return portrait.DisplayName ?? portrait.Id.ToString();
    }

    public bool IsAuthored(string key) => Pack.Portraits.Any(p => Pack.Key(p.Id) == key);

    public IReadOnlySet<string> RemovedKeys => _saved.Portraits.Select(p => Pack.Key(p.Id))
        .Except(Pack.Portraits.Select(p => Pack.Key(p.Id))).ToHashSet();

    public void Delete(string key, IReadOnlyCollection<string> users)
    {
        if (!IsAuthored(key)) throw new InvalidOperationException("固有头像不能删除。");
        if (users.Count > 0) throw new InvalidOperationException("头像仍在使用：" + string.Join("、", users));
        Change(Pack with { Portraits = Pack.Portraits.Where(p => Pack.Key(p.Id) != key).ToArray() });
    }

    public void Replace(string key, PngImage image)
    {
        ValidateImage(image);
        _ = Portrait(key);
        string path = $"images/{Guid.NewGuid():N}.png";
        var file = new ImageFile(path, image.Width, image.Height, 3);
        var bust = PortraitGeometry.DefaultBust.SnapToPixels(PortraitView.Bust, image.Width, image.Height);
        var face = PortraitGeometry.FaceFromBust(PortraitGeometry.DefaultBust).SnapToPixels(PortraitView.Face, image.Width, image.Height);
        var next = IsAuthored(key)
            ? Pack with { Portraits = Pack.Portraits.Select(p => Pack.Key(p.Id) == key
                ? p with { Image = file, BustAnchor = bust, FaceAnchor = face } : p).ToArray() }
            : Pack with { Overrides = [.. Pack.Overrides.Where(p => p.Target != key), new(key, bust, face) { Image = file }] };
        byte[] bytes = image.Encode();
        Change(next); _images[path] = bytes; _ownedImages.Add(path);
    }

    public void RestoreOriginal(string key)
    {
        if (IsAuthored(key)) throw new InvalidOperationException("自建头像没有原版图片。");
        _ = _baseByKey[key];
        Change(Pack with { Overrides = Pack.Overrides.Where(p => p.Target != key).ToArray() });
    }

    public void Rename(string key, string name)
    {
        var authored = Pack.Portraits.SingleOrDefault(p => Pack.Key(p.Id) == key)
            ?? throw new InvalidOperationException("只能修改自建头像的名称。");
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("头像名称不能为空。");
        name = name.Trim();
        Change(Pack with { Portraits = Pack.Portraits.Select(p => p.Id == authored.Id ? p with { Name = name } : p).ToArray() });
    }

    public IReadOnlySet<string> ModifiedKeys
    {
        get
        {
            return Catalog.Portraits.Where(p => !_savedPortraits.TryGetValue(p.Image.Id, out var old) || p != old)
                .Select(p => p.Image.Id).ToHashSet();
        }
    }

    public void SetAnchor(string key, PortraitView view, PortraitAnchor anchor)
    {
        var current = Portrait(key);
        SetFraming(key, current.WithAnchor(view, anchor.SnapToPixels(view, current.Image.Width, current.Image.Height)));
    }

    public void SetAnchors(string key, PortraitAnchor bust, PortraitAnchor face)
    {
        var current = Portrait(key);
        SetFraming(key, current with { BustAnchor = bust, FaceAnchor = face });
    }

    public void ApplyAutomaticFace(string key, PortraitAnchor face)
    {
        var current = Portrait(key);
        face = face.SnapToPixels(PortraitView.Face, current.Image.Width, current.Image.Height);
        var bust = PortraitGeometry.BustFromFace(face).SnapToPixels(PortraitView.Bust, current.Image.Width, current.Image.Height);
        SetFraming(key, current with { FaceAnchor = face, BustAnchor = bust });
    }

    private void SetFraming(string key, PortraitDefinition portrait)
    {
        portrait.Validate();
        var authored = Pack.Portraits.SingleOrDefault(p => Pack.Key(p.Id) == key);
        if (authored is not null)
            Change(Pack with { Portraits = Pack.Portraits.Select(p => p.Id == authored.Id
                ? p with { BustAnchor = portrait.BustAnchor, FaceAnchor = portrait.FaceAnchor } : p).ToArray() });
        else
        {
            var original = _baseByKey[key];
            var other = Pack.Overrides.Where(p => p.Target != key);
            var replacement = Pack.Overrides.SingleOrDefault(p => p.Target == key)?.Image;
            Change(Pack with { Overrides = replacement is null && portrait.BustAnchor == original.BustAnchor && portrait.FaceAnchor == original.FaceAnchor
                ? other.ToArray() : [.. other, new(key, portrait.BustAnchor, portrait.FaceAnchor) { Image = replacement }] });
        }
    }

    public void Restore(string key, bool basis)
    {
        var authored = Pack.Portraits.SingleOrDefault(p => Pack.Key(p.Id) == key);
        if (authored is not null)
        {
            var baseline = _opened.Portraits.SingleOrDefault(p => p.Id == authored.Id) ?? _created[authored.Id];
            var restored = basis ? authored with { BustAnchor = PortraitGeometry.DefaultBust,
                FaceAnchor = PortraitGeometry.FaceFromBust(PortraitGeometry.DefaultBust) } : baseline;
            Change(Pack with { Portraits = Pack.Portraits.Select(p => p.Id == authored.Id ? restored : p).ToArray() });
            return;
        }
        if (basis)
        {
            var original = _baseByKey[key];
            SetFraming(key, original);
        }
        else Change(Pack with { Overrides = [.. Pack.Overrides.Where(p => p.Target != key), .. _opened.Overrides.Where(p => p.Target == key)] });
    }

    public void Undo() { if (_undo.TryPop(out var pack)) { _redo.Push(Pack); Pack = pack; } }
    public void Redo() { if (_redo.TryPop(out var pack)) { _undo.Push(Pack); Pack = pack; } }
    private void Change(PortraitPack pack)
    {
        pack.Validate();
        if (Equal(Pack, pack)) return;
        _undo.Push(Pack); _redo.Clear(); Pack = pack;
    }
    private static bool Equal(PortraitPack a, PortraitPack b) => a.Id == b.Id && a.SchemaVersion == b.SchemaVersion &&
        a.Portraits.SequenceEqual(b.Portraits) && a.Overrides.SequenceEqual(b.Overrides);

    public void Save() => Save(null);

    public void Save(IReadOnlySet<string>? usedKeys)
    {
        if (!HasChanges) return;
        var removed = RemovedKeys;
        if (removed.Count > 0 && usedKeys is null) throw new InvalidOperationException("删除头像前必须重新核对使用者。");
        if (removed.Any(key => usedKeys!.Contains(key))) throw new InvalidOperationException("待删除头像仍在使用，请先更换使用者的头像。");
        _ = Catalog;
        CheckSnapshot();
        var created = new List<string>();
        try
        {
            foreach (var entry in Pack.ImageFiles().Distinct())
            {
                string path = PackageFiles.Resolve(Root, entry.Path);
                if (_images.TryGetValue(entry.Path, out var bytes) && !File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    created.Add(path); output.Write(bytes); output.Flush(true);
                }
                var image = PngImage.Decode(File.ReadAllBytes(path));
                if (image.Width != entry.Width || image.Height != entry.Height || image.Channels != entry.Channels)
                    throw new InvalidDataException($"Portrait image metadata mismatch: {entry.Path}");
            }
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(Pack, PackageFiles.JsonOptions);
            AtomicFile.Write(ManifestPath, manifest, CheckSnapshot);
            _expected = manifest; _saved = Pack; _savedPortraits = _portraits;
            _undo.Clear(); _redo.Clear();
            var baselines = _created.Values.Select(p => p.Image.Path).ToHashSet();
            foreach (string path in _images.Keys.Where(p => !baselines.Contains(p) || File.Exists(PackageFiles.Resolve(Root, p))).ToArray()) _images.Remove(path);
        }
        catch
        {
            foreach (string path in created) File.Delete(path);
            throw;
        }
    }

    public void Dispose()
    {
        // Only remove images observed in this session, after the published manifest has been verified.
        // Keep opening baselines alive until close so Restore remains valid after saving.
        try { CheckSnapshot(); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        var keep = _saved.ImageFiles().Select(p => p.Path).ToHashSet();
        foreach (string relative in _ownedImages.Where(p => !keep.Contains(p)))
        {
            try { File.Delete(PackageFiles.Resolve(Root, relative)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void ValidateImage(PngImage image)
    {
        if (image.Width != image.Height || image.Width < 16 || image.Channels != 3)
            throw new InvalidDataException("头像须为至少16×16的方形RGB图片。");
    }

    private void CheckSnapshot()
    {
        byte[]? current = File.Exists(ManifestPath) ? File.ReadAllBytes(ManifestPath) : null;
        if (current is null != (_expected is null) || current is not null && !current.AsSpan().SequenceEqual(_expected))
            throw new IOException("头像包已被外部修改，请重新打开编辑器。");
    }
}

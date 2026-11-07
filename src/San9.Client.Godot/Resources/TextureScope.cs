using Godot;
using San9.Content.Images;
using San9.Rendering;

namespace San9.Client.Godot;

public sealed class TextureScope(TextureService service, IReadOnlyDictionary<string, ImageAsset> assets)
    : IDisposable
{
    private sealed class Binding(ImageAsset asset, TextureCache<ImageTexture>.Lease lease)
    {
        public readonly ImageAsset Asset = asset;
        public readonly AtlasTexture View = new() { Region = new(asset.X, asset.Y, asset.Width, asset.Height), FilterClip = true };
        public readonly TextureCache<ImageTexture>.Lease Lease = lease;
        public bool Installed;
        public Exception? Error;
    }
    private readonly Dictionary<string, Binding> _bindings = [];
    private readonly List<Binding> _pending = [], _failed = [];
    private int _remaining;
    private bool _disposed;
    private readonly Dictionary<CanvasItem, Action> _consumers = [];
    public long Revision { get; private set; }
    public TextureService Service => service;
    public bool IsReady => _remaining == 0;
    public Exception? Error => _failed.Count == 0 ? null : _failed[0].Error;
    internal bool NeedsRefresh => _pending.Count > 0;
    public IReadOnlyDictionary<string, ImageAsset> Assets => assets;
    public int BindingCount => _bindings.Count;
    internal int RedrawConsumerCount => _consumers.Count;
    /// <summary>Access a resource for skin composition. Drawing users must bind their own canvas item.</summary>
    internal Texture2D GetResource(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bindings.TryGetValue(key, out var binding) ? binding.View :
            throw new InvalidOperationException($"Texture '{key}' must be requested before binding or drawing.");
    }
    /// <summary>Bind the actual drawing node, independently of the scope's creator or scene parent.</summary>
    public Texture2D Get(string key, CanvasItem consumer)
    {
        var texture = GetResource(key);
        BindRedraw(consumer);
        return texture;
    }
    public void BindRedraw(CanvasItem consumer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_consumers.ContainsKey(consumer)) AttachConsumer(consumer);
    }
    private void AttachConsumer(CanvasItem consumer)
    {
        void Detach()
        {
            consumer.TreeExiting -= Detach;
            _consumers.Remove(consumer);
        }
        _consumers.Add(consumer, Detach);
        consumer.TreeExiting += Detach;
    }
    private void Request(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_bindings.ContainsKey(key)) return;
        var asset = assets[key];
        var binding = new Binding(asset, service.Cache.Acquire([asset.File]));
        _bindings.Add(key, binding); _remaining++;
        if (!Install(binding)) { _pending.Add(binding); service.ScheduleRefresh(this); }
    }
    public void RequestPrefixes(params string[] prefixes) => Request(assets.Keys.Where(key =>
        !TextureGroups.IsPortrait(key) && prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal))));
    public bool AreReady(IEnumerable<string> keys) => keys.All(k => _bindings.TryGetValue(k, out var b) && b.Installed);
    public Exception? ErrorFor(IEnumerable<string> keys)
    {
        foreach (string key in keys)
            if (_bindings.TryGetValue(key, out var binding) && binding.Error is { } error) return error;
        return null;
    }
    public bool TryGetReady(string key, CanvasItem consumer, out Texture2D texture)
    {
        BindRedraw(consumer);
        return TryGetReadyResource(key, out texture);
    }
    /// <summary>Ready resource for native controls, cursor installation and sprite composition; no custom draw subscription.</summary>
    internal bool TryGetReadyResource(string key, out Texture2D texture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_bindings.TryGetValue(key, out var binding) && binding.Installed)
        { texture = binding.View; return true; }
        texture = null!; return false;
    }
    public void RequestCached(IEnumerable<string> keys)
    {
        foreach (string key in keys)
            if (service.Cache.Contains(assets[key].File)) Request(key);
    }
    public void Request(IEnumerable<string> keys) { foreach (string key in keys) Request(key); }

    /// <summary>After an atomic camera publication, retire bindings outside its new visible dependency set.</summary>
    internal void Retain(IReadOnlySet<string> keys)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (string key in _bindings.Keys.Where(key => !keys.Contains(key)).ToArray())
        {
            var binding = _bindings[key];
            _bindings.Remove(key); _pending.Remove(binding); _failed.Remove(binding);
            if (!binding.Installed) _remaining--;
            service.Defer(() => Release(binding));
        }
    }
    public void RetryFailed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        service.RetryFailed(_failed.Select(b => b.Asset.File));
    }
    internal void RetryFailed(IReadOnlySet<ImageFile> files)
    {
        for (int i = _failed.Count - 1; i >= 0; i--)
        {
            var binding = _failed[i];
            if (!files.Contains(binding.Asset.File)) continue;
            binding.Error = null;
            _pending.Add(binding); _failed.RemoveAt(i);
        }
        if (NeedsRefresh) service.ScheduleRefresh(this);
    }
    public void Refresh()
    {
        bool changed = false;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (!Install(_pending[i])) continue;
            _pending.RemoveAt(i); changed = true;
        }
        if (changed)
            foreach (var consumer in _consumers.Keys)
                if (GodotObject.IsInstanceValid(consumer) && consumer.IsInsideTree()) consumer.QueueRedraw();
    }
    // A binding leaves the pending set once installed or failed; ready leases never need polling.
    private bool Install(Binding binding)
    {
        if (binding.Lease.IsReady)
        {
            binding.View.Atlas = service.Cache.Get(binding.Asset.File);
            binding.Installed = true; _remaining--; Revision++;
            return true;
        }
        if (binding.Lease.Error is not { } error) return false;
        binding.Error = error; _failed.Add(binding);
        GD.PushError($"Texture {binding.Asset.Id}: {error.Message}");
        return true;
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        foreach (var (consumer, detach) in _consumers)
            if (GodotObject.IsInstanceValid(consumer)) consumer.TreeExiting -= detach;
        _consumers.Clear();
        service.Retire(this, () =>
        {
            foreach (var b in _bindings.Values) Release(b);
            _bindings.Clear();
        });
        _pending.Clear(); _failed.Clear();
    }

    private static void Release(Binding binding)
    {
        binding.View.Atlas = null; binding.View.Dispose();
        binding.Lease.Dispose();
    }
}

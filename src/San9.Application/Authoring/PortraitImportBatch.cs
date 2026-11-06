using San9.Content.Images;

namespace San9.Application.Authoring;

/// <summary>Resolves name collisions once per import batch and keeps newly added names in that index.</summary>
public sealed class PortraitImportBatch
{
    private readonly PortraitEditorWorkspace _workspace;
    private readonly Dictionary<string, List<string>>? _targets;

    public PortraitImportBatch(PortraitEditorWorkspace workspace, bool overwriteSameName)
    {
        _workspace = workspace;
        if (overwriteSameName)
            _targets = workspace.Catalog.Portraits.GroupBy(p => p.DisplayName ?? p.Id.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(p => p.Image.Id).ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public string Import(string name, PngImage image)
    {
        name = name.Trim();
        if (_targets is not null && _targets.TryGetValue(name, out var matches))
        {
            if (matches.Count != 1) throw new InvalidOperationException("存在多个同名头像，请先改名或指定替换目标。");
            _workspace.Replace(matches[0], image);
            return matches[0];
        }
        string key = _workspace.Add(name, image);
        _targets?.Add(name, [key]);
        return key;
    }
}

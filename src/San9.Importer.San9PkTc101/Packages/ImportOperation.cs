namespace San9.Importer.San9PkTc101.Packages;

/// <summary>Exclusive writer ownership of an import output, shared by every public import entry.</summary>
internal sealed class ImportOperation : IDisposable
{
    private readonly FileStream _ownership;
    private readonly string _root;
    private readonly string _marker;
    private bool _disposed;
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private ImportOperation(string output, ImportOperation? parent)
    {
        _root = Path.TrimEndingDirectorySeparator(output);
        _marker = Path.Combine(_root, ".import.lock");
        if (parent is not null)
        {
            ObjectDisposedException.ThrowIf(parent._disposed, parent);
            if (!IsInside(parent._root, _root)) throw new ArgumentException("A nested import must belong to its parent output.");
        }
        Directory.CreateDirectory(output);
        _ownership = new(_marker, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        try
        {
            // Acquire our marker before probing neighbors. A parent and child racing to
            // acquire ownership therefore see one another rather than both proceeding.
            for (var directory = Directory.GetParent(_root); directory is not null; directory = directory.Parent)
            {
                string marker = Path.Combine(directory.FullName, ".import.lock");
                if (parent is not null && marker.Equals(parent._marker, PathComparison)) continue;
                Probe(marker);
            }
            foreach (string marker in Directory.EnumerateFiles(_root, ".import.lock", new EnumerationOptions
                { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
                if (!marker.Equals(_marker, PathComparison)) Probe(marker);
        }
        catch { _ownership.Dispose(); throw; }
    }

    private static void Probe(string marker)
    {
        if (!File.Exists(marker)) return;
        try { using var guard = new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        catch (FileNotFoundException) { } // An owner may finish between discovery and opening.
    }

    private static bool IsInside(string root, string path) => path.Equals(root, PathComparison)
        || path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, PathComparison);

    internal void RequireOutput(string output)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsInside(_root, Path.GetFullPath(output))) throw new InvalidOperationException("Import output is outside its writer ownership.");
    }

    internal static ImportOperation Acquire(string output, ImportOperation? parent = null) => new(Path.GetFullPath(output), parent);
    public void Dispose() { _ownership.Dispose(); _disposed = true; }
}

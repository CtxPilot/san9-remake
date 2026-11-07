using System.Text.Json;

namespace San9.Content.Packages;

/// <summary>One publication boundary for related content files. Compilation never replaces live inputs.</summary>
public sealed class ContentPublication : IDisposable
{
    private readonly List<(string Pending, string Target)> _files = [];
    private readonly HashSet<string> _targets = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly List<Action> _cleanup = [];
    private bool _finished;
    private bool _failed;

    public async Task StageAsync<T>(string target, T value, JsonSerializerOptions options, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_finished, this);
        if (_failed) throw new InvalidOperationException("Cannot stage a failed content publication.");
        try
        {
            target = Path.GetFullPath(target);
            if (!_targets.Add(target)) throw new InvalidDataException($"Duplicate publication target: {target}");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string pending = target + ".pending";
            _files.Add((pending, target));
            await using var stream = File.Create(pending);
            await JsonSerializer.SerializeAsync(stream, value, options, token);
        }
        catch { _failed = true; throw; }
    }

    internal void AfterCommit(Action cleanup)
    {
        ObjectDisposedException.ThrowIf(_finished, this);
        _cleanup.Add(cleanup);
    }

    public void Retire(string path) => AfterCommit(() => File.Delete(path));

    public void Commit(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_finished, this);
        if (_failed) throw new InvalidOperationException("Cannot commit a failed content publication.");
        token.ThrowIfCancellationRequested();
        try { PublishFiles(_files); }
        catch { _failed = true; throw; }
        _finished = true;
        foreach (var cleanup in _cleanup) cleanup();
    }

    public void Dispose()
    {
        foreach (var (pending, _) in _files) File.Delete(pending);
        _files.Clear(); _cleanup.Clear(); _targets.Clear(); _finished = true;
    }

    internal static void PublishFiles(IReadOnlyList<(string Pending, string Target)> files)
    {
        List<(string Target, string? Backup)> replaced = [];
        List<string> backups = [];
        bool complete = false;
        try
        {
            foreach (var (pending, target) in files)
            {
                string? backup = File.Exists(target) ? target + ".previous" : null;
                if (backup is not null)
                {
                    File.Copy(target, backup, true);
                    backups.Add(backup);
                }
                File.Move(pending, target, true);
                replaced.Add((target, backup));
            }
            complete = true;
        }
        catch
        {
            foreach (var (target, backup) in replaced.AsEnumerable().Reverse())
                if (backup is null) File.Delete(target);
                else File.Move(backup, target, true);
            complete = true;
            throw;
        }
        finally
        {
            if (complete) foreach (string backup in backups) File.Delete(backup);
        }
    }

}

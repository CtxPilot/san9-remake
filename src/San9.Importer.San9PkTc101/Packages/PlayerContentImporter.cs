using System.Diagnostics;

namespace San9.Importer.San9PkTc101.Packages;

/// <summary>Complete player import, independent of any client or console host.</summary>
public static class PlayerContentImporter
{
    public static async Task ImportAsync(string gameDirectory, string output, string? ffmpeg = null, bool audio = true, bool movies = true,
        Action<string>? progress = null, CancellationToken token = default)
    {
        var source = new San9SourceDirectory(gameDirectory);
        output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(output));
        string sourceRoot = Path.TrimEndingDirectorySeparator(source.Root);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (Within(output, sourceRoot) || Within(sourceRoot, output))
            throw new ArgumentException("原版目录与内容目录必须相互独立，不能相同或互相包含。");

        bool Within(string path, string root) => path.Equals(root, comparison) ||
            path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, comparison);

        progress?.Invoke("检查原版版本：仅支持繁体中文三国志 IX 威力加强版 1.0.1.0 的已登记构建。");
        var identity = await San9PkTc101Detector.DetectAsync(source.Executable, token);
        if (!identity.IsMatch) throw new InvalidDataException("原版版本不受支持：" + identity.Reason);
        string? encoder = null;
        if (audio || movies)
        {
            encoder = ResolveEncoder(ffmpeg);
            await CheckEncoderAsync(encoder, movies, token);
        }
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(output);
        await using var ownership = new FileStream(Path.Combine(output, ".import.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);

        progress?.Invoke("[1/4] 转换纹理与界面资源……");
        await TexturePackageImporter.ImportAsync(source.Root, Path.Combine(output, "shared", "textures"), progress, token);
        if (audio)
        {
            progress?.Invoke("[2/4] 转换音乐与音效……");
            await AudioLibraryImporter.ImportAsync(source.Root, output, AudioLibraryGroup.All, progress, token, ffmpegPath: encoder);
        }
        else progress?.Invoke("[2/4] 本次不转换音频，保留已有音乐与音效。");
        progress?.Invoke("[3/4] 转换全部剧本……");
        await ContentCatalogImporter.ImportAsync(source.Root, output, progress, token);
        if (movies)
        {
            progress?.Invoke("[4/4] 转换影片……");
            if (!await MovieLibraryImporter.ImportAsync(source.Root, output, MovieLibraryGroup.All, progress, token, ffmpegPath: encoder))
                throw new InvalidOperationException("找不到 FFmpeg，影片转换未完成。");
        }
        else progress?.Invoke("[4/4] 本次不转换影片，保留已有影片。");
        token.ThrowIfCancellationRequested();
        progress?.Invoke("资源转换完成，等待内容校验。");
    }

    private static string ResolveEncoder(string? selected)
    {
        string name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        if (!string.IsNullOrWhiteSpace(selected))
        {
            string path = Path.GetFullPath(selected);
            if (!File.Exists(path) || !Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException($"请选择 {name} 可执行文件。", path);
            return path;
        }
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string path = Path.Combine(directory.Trim().Trim('"'), name);
            if (File.Exists(path)) return Path.GetFullPath(path);
        }
        throw new FileNotFoundException("未找到 FFmpeg。请先安装 FFmpeg，或在界面中选择 ffmpeg 可执行文件后重试。");
    }

    private static async Task CheckEncoderAsync(string executable, bool movies, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "-nostdin", "-hide_banner", "-encoders" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("无法启动 FFmpeg。");
        using var registration = token.Register(() => { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException)
        {
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }
        string encoders = await stdout;
        string diagnostics = await stderr;
        if (process.ExitCode != 0 || !encoders.Contains("libvorbis", StringComparison.Ordinal) || (movies && !encoders.Contains("libtheora", StringComparison.Ordinal)))
            throw new InvalidDataException("FFmpeg 必须支持 libvorbis 编码器，转换影片还需要 libtheora。" + diagnostics);
    }
}

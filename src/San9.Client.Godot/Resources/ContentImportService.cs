namespace San9.Client.Godot;

/// <summary>The optional export-only content preparation adapter; game loading still consumes converted packages.</summary>
internal static class ContentImportService
{
    internal static bool Available =>
#if SAN9_EXPORT_IMPORTER
        true;
#else
        false;
#endif

    internal static Task ImportAsync(string source, string output, string? ffmpeg, bool audio, bool movies,
        Action<string> progress, CancellationToken token)
    {
#if SAN9_EXPORT_IMPORTER
        return Task.Run(async () =>
        {
            await San9.Importer.San9PkTc101.Packages.PlayerContentImporter.ImportAsync(source, output, ffmpeg, audio, movies, progress, token);
            progress("校验转换内容……");
            await San9.Application.Diagnostics.ContentVerification.VerifyAsync(output, progress, token);
            token.ThrowIfCancellationRequested();
        }, token);
#else
        throw new InvalidOperationException("开发构建不包含内置转换器，请使用 San9.Tools。");
#endif
    }
}

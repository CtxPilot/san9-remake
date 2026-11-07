using San9.Application.Diagnostics;
using San9.Importer.San9PkTc101.Packages;

internal static class PlayerContentPreparation
{
    internal static async Task RunAsync(string gameDirectory, string output, string? ffmpeg, bool audio, bool movies)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, args) => { args.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            await PlayerContentImporter.ImportAsync(gameDirectory, output, ffmpeg, audio, movies, Console.WriteLine, cancellation.Token);
            Console.WriteLine("校验转换内容……");
            await ContentVerification.VerifyAsync(output, Console.WriteLine, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Console.WriteLine("转换完成，可以进入游戏。");
        }
        finally { Console.CancelKeyPress -= cancel; }
    }
}

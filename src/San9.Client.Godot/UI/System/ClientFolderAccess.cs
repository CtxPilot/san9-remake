using Godot;

namespace San9.Client.Godot;

internal static class ClientFolderAccess
{
    public static void Open(string directory)
    {
        string path = Path.GetFullPath(directory);
        Directory.CreateDirectory(path);
        var error = OS.ShellOpen(new Uri(path + Path.DirectorySeparatorChar).AbsoluteUri);
        if (error != Error.Ok) throw new InvalidOperationException($"{path} ({error})");
    }
}

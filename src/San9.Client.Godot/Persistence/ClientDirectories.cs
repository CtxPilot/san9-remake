namespace San9.Client.Godot.Persistence;

/// <summary>An immutable startup snapshot shared by the opening menu and every game session.</summary>
public sealed record ClientDirectories(string ContentRoot, ClientDataPaths UserData)
{
    public static ClientDirectories Resolve(bool exported, string platform, string executablePath,
        string projectDirectory, string engineUserDirectory, string documentsDirectory)
    {
        if (exported && platform == "macOS")
        {
            string root = Path.GetFullPath(Path.Combine(documentsDirectory, "San9Remake"));
            return new(Path.Combine(root, "content"), new(Path.Combine(root, "userdata")));
        }
        string content = exported ? Path.Combine(Path.GetDirectoryName(executablePath)!, "content")
            : Path.Combine(projectDirectory, "../../local/generated/content");
        string userData = exported && platform is "Windows" or "Linux"
            ? Path.Combine(Path.GetDirectoryName(executablePath)!, "userdata") : engineUserDirectory;
        return new(Path.GetFullPath(content), new(Path.GetFullPath(userData)));
    }

    public ClientDirectories ApplyOverrides(ClientOptions options) => this with
    {
        ContentRoot = options.ContentRoot,
        UserData = options.UserDataRoot is { } root ? new(root) : UserData
    };
}

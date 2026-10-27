using San9.Client.Godot.Persistence;

namespace San9.Client.Godot.Tests;

public sealed class ClientDirectoriesTests
{
    private static string At(params string[] parts) => Path.Combine([Path.GetTempPath(), "san9-path-policy", .. parts]);

    [Theory]
    [InlineData("Windows")]
    [InlineData("Linux")]
    public void DesktopExportsKeepContentAndUserDataBesideTheExecutable(string platform)
    {
        var paths = ClientDirectories.Resolve(true, platform, At("install", "San9"), At("project"), At("engine-user"), At("documents"));
        Assert.Equal(At("install", "content"), paths.ContentRoot);
        Assert.Equal(At("install", "userdata"), paths.UserData.Root);
    }

    [Fact]
    public void MacExportUsesPlayerDocumentsIndependentlyOfAppLocation()
    {
        var paths = ClientDirectories.Resolve(true, "macOS", At("randomized", "San9.app", "Contents", "MacOS", "San9"),
            At("project"), At("engine-user"), At("documents"));
        Assert.Equal(At("documents", "San9Remake", "content"), paths.ContentRoot);
        Assert.Equal(At("documents", "San9Remake", "userdata", "saves", "quick.save.json"), paths.UserData.QuickSave);
        var installed = ClientDirectories.Resolve(true, "macOS", At("Applications", "San9.app", "Contents", "MacOS", "San9"),
            At("project"), At("engine-user"), At("documents"));
        Assert.Equal(paths.ContentRoot, installed.ContentRoot);
        Assert.Equal(paths.UserData.Root, installed.UserData.Root);
    }

    [Theory]
    [InlineData("Windows")]
    [InlineData("Linux")]
    [InlineData("macOS")]
    public void EditorKeepsRepositoryContentAndGodotUserStorage(string platform)
    {
        var paths = ClientDirectories.Resolve(false, platform, At("editor"), At("repo", "src", "client"), At("engine-user"), At("documents"));
        Assert.Equal(At("repo", "local", "generated", "content"), paths.ContentRoot);
        Assert.Equal(At("engine-user"), paths.UserData.Root);
    }

    [Fact]
    public void ExplicitOverridesArePerLaunchAndDoNotChangeTheDefaults()
    {
        var paths = ClientDirectories.Resolve(true, "macOS", At("app", "San9"), At("project"), At("engine-user"), At("documents"));
        var contentOnly = paths.ApplyOverrides(ClientOptions.Parse(["--content-root", At("mod")], paths.ContentRoot));
        Assert.Equal(At("mod"), contentOnly.ContentRoot);
        Assert.Equal(paths.UserData.Root, contentOnly.UserData.Root);
        var userOnly = paths.ApplyOverrides(ClientOptions.Parse(["--user-data-root", At("test-user")], paths.ContentRoot));
        Assert.Equal(paths.ContentRoot, userOnly.ContentRoot);
        Assert.Equal(At("test-user"), userOnly.UserData.Root);
        Assert.Equal(At("documents", "San9Remake", "userdata"), paths.UserData.Root);
    }
}

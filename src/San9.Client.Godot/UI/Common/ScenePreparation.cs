namespace San9.Client.Godot;

/// <summary>The opening must be visible before preparation; revealing waits for both independent lifetimes.</summary>
public static class ScenePreparation
{
    public static async Task<T> PrepareAsync<T>(Task openingPresented, Task openingFinished,
        Func<Task<T>> prepare, CancellationToken token)
    {
        await openingPresented.WaitAsync(token);
        token.ThrowIfCancellationRequested();
        T scene = await prepare();
        await openingFinished.WaitAsync(token);
        return scene;
    }
}

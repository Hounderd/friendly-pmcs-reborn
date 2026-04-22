namespace FriendlyPMC.CoreFollowers.Threading;

public static class BundlePreloadGuard
{
    public static async Task<bool> RunAsync(Func<Task> preload)
    {
        try
        {
            await preload();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

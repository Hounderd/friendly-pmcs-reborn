using System.Threading;

namespace FriendlyPMC.CoreFollowers.Threading;

public static class SynchronizationContextBridge
{
    public static Task ResumeOnAsync(SynchronizationContext? targetContext)
    {
        if (targetContext is null || ReferenceEquals(SynchronizationContext.Current, targetContext))
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        targetContext.Post(_ => completion.TrySetResult(true), null);
        return completion.Task;
    }
}

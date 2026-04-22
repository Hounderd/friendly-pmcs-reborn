namespace FriendlyPMC.CoreFollowers.Services;

public sealed class CustomFollowerNavigationController
{
    public CustomFollowerNavigationIntent CurrentIntent { get; private set; } = CustomFollowerNavigationIntent.None;

    public bool RequiresRecovery => CurrentIntent == CustomFollowerNavigationIntent.RepathAndRecover;

    public CustomFollowerNavigationIntent Update(CustomFollowerBrainDecision decision)
    {
        CurrentIntent = CustomFollowerNavigationPolicy.Resolve(decision);
        return CurrentIntent;
    }
}

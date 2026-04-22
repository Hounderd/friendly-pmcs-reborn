namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerMovementActivationPolicy
{
    public static bool TryRunBossFindAction(Action? bossFindAction)
    {
        if (bossFindAction is null)
        {
            return false;
        }

        try
        {
            bossFindAction();
            return true;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }
}

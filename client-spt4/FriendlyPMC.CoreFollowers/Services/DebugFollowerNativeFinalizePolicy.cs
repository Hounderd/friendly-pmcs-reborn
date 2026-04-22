namespace FriendlyPMC.CoreFollowers.Services;

public static class DebugFollowerNativeFinalizePolicy
{
    public static bool ShouldInvokeNativeFinalize(bool manualFollowerBindingOwnsInitialization)
    {
        return !manualFollowerBindingOwnsInitialization;
    }
}

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerHoldStatePolicy
{
    public static bool ShouldRefreshHold(
        FollowerModeDisposition disposition,
        bool hasWaitRequest,
        bool isHoldLayerActive)
    {
        return disposition == FollowerModeDisposition.ForceHoldAnchor
            && (!hasWaitRequest || !isHoldLayerActive);
    }
}

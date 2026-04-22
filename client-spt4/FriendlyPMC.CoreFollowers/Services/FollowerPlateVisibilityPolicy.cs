namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerPlateVisibilityPolicy
{
    public static bool ShouldShowPlate(
        bool isEnabled,
        bool isOperational,
        float distanceToPlayerMeters,
        float maxDistanceMeters)
    {
        return isEnabled
            && isOperational
            && distanceToPlayerMeters <= maxDistanceMeters;
    }
}

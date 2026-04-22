namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerSuppressionEngagementPolicy
{
    public const float DefaultVisibleThreatSuppressionBreakDistanceMeters = 18f;
    public const float DefaultShootableThreatSuppressionBreakDistanceMeters = 25f;

    public static bool ShouldUseSuppression(
        bool targetVisible,
        bool canShoot,
        float distanceToNearestActionableEnemyMeters)
    {
        if (targetVisible
            && distanceToNearestActionableEnemyMeters <= DefaultVisibleThreatSuppressionBreakDistanceMeters)
        {
            return false;
        }

        if (canShoot
            && distanceToNearestActionableEnemyMeters <= DefaultShootableThreatSuppressionBreakDistanceMeters)
        {
            return false;
        }

        return true;
    }
}

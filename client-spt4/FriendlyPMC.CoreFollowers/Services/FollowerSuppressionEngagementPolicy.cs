namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerSuppressionEngagementPolicy
{
    public const float DefaultBlindThreatAttackCloseDistanceMeters = 30f;
    public const float DefaultVisibleThreatSuppressionBreakDistanceMeters = 18f;
    public const float DefaultShootableThreatSuppressionBreakDistanceMeters = 40f;

    public static bool ShouldUseAttackCloseWithoutSight(
        bool targetVisible,
        bool canShoot,
        float distanceToNearestActionableEnemyMeters)
    {
        return !targetVisible
            && !canShoot
            && distanceToNearestActionableEnemyMeters <= DefaultBlindThreatAttackCloseDistanceMeters;
    }

    public static bool ShouldUseSuppression(
        bool targetVisible,
        bool canShoot,
        float distanceToNearestActionableEnemyMeters)
    {
        if (targetVisible && !canShoot)
        {
            return false;
        }

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

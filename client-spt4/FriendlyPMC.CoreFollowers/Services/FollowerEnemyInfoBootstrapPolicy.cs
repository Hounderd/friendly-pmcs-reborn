namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerEnemyInfoBootstrapDecision(
    bool ShouldCreateEnemyInfoFallback);

public static class FollowerEnemyInfoBootstrapPolicy
{
    public static FollowerEnemyInfoBootstrapDecision Resolve(bool hasEnemyInfoAfterGroupReport)
    {
        return new FollowerEnemyInfoBootstrapDecision(
            ShouldCreateEnemyInfoFallback: !hasEnemyInfoAfterGroupReport);
    }
}

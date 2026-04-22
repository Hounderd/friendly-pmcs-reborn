namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerAttentionResetDecision(
    bool ShouldClearGoalEnemy);

public static class FollowerAttentionResetPolicy
{
    public const float DefaultStaleGoalEnemyAgeSeconds = 2.5f;

    public static FollowerAttentionResetDecision Evaluate(
        bool haveEnemy,
        bool goalEnemyVisible,
        bool goalEnemyCanShoot,
        float goalEnemyLastSeenAgeSeconds)
    {
        var shouldClearGoalEnemy = haveEnemy
            && !goalEnemyVisible
            && !goalEnemyCanShoot
            && (goalEnemyLastSeenAgeSeconds < 0f
                || goalEnemyLastSeenAgeSeconds >= DefaultStaleGoalEnemyAgeSeconds);

        return new FollowerAttentionResetDecision(shouldClearGoalEnemy);
    }
}

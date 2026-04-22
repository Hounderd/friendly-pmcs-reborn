namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerEnemyLivenessState(
    string? ProfileId,
    bool IsDead,
    bool IsGoalEnemy,
    bool IsLastEnemy);

public readonly record struct FollowerEnemyLivenessCleanupDecision(
    bool ShouldClearAnyEnemyMemory,
    IReadOnlyList<string> ProfileIdsToForget,
    bool ShouldClearGoalEnemy,
    bool ShouldClearLastEnemy);

public static class FollowerEnemyLivenessCleanupPolicy
{
    public static FollowerEnemyLivenessCleanupDecision Evaluate(IEnumerable<FollowerEnemyLivenessState> enemies)
    {
        var deadEnemies = enemies
            .Where(enemy => enemy.IsDead && !string.IsNullOrWhiteSpace(enemy.ProfileId))
            .ToArray();
        if (deadEnemies.Length == 0)
        {
            return default;
        }

        return new FollowerEnemyLivenessCleanupDecision(
            ShouldClearAnyEnemyMemory: true,
            ProfileIdsToForget: deadEnemies.Select(enemy => enemy.ProfileId!).Distinct(StringComparer.Ordinal).ToArray(),
            ShouldClearGoalEnemy: deadEnemies.Any(enemy => enemy.IsGoalEnemy),
            ShouldClearLastEnemy: deadEnemies.Any(enemy => enemy.IsLastEnemy));
    }
}

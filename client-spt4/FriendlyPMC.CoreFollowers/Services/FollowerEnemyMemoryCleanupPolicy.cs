using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerEnemyMemoryState(
    string? ProfileId,
    bool IsGoalEnemy,
    bool IsVisible,
    bool CanShoot,
    float LastSeenAgeSeconds,
    bool IsProtected);

public readonly record struct FollowerEnemyMemoryCleanupDecision(
    bool ShouldClearGoalEnemy,
    string[] ProfileIdsToForget)
{
    public bool ShouldClearAnyEnemyMemory => ProfileIdsToForget.Length > 0 || ShouldClearGoalEnemy;
}

public static class FollowerEnemyMemoryCleanupPolicy
{
    public const float DefaultStaleEnemyLastSeenThresholdSeconds = 5f;

    public static FollowerEnemyMemoryCleanupDecision Evaluate(
        FollowerCommand command,
        CustomFollowerBrainMode mode,
        bool hasActionableEnemy,
        bool hasFreshThreatContext,
        IEnumerable<FollowerEnemyMemoryState> enemies)
    {
        if (command == FollowerCommand.Combat
            || mode == CustomFollowerBrainMode.CombatPursue
            || hasActionableEnemy
            || hasFreshThreatContext)
        {
            return new FollowerEnemyMemoryCleanupDecision(false, Array.Empty<string>());
        }

        var profileIdsToForget = enemies
            .Where(ShouldForgetEnemyMemory)
            .Select(enemy => enemy.ProfileId)
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToArray();

        var shouldClearGoalEnemy = enemies.Any(enemy =>
            enemy.IsGoalEnemy
            && ShouldForgetEnemyMemory(enemy));

        return new FollowerEnemyMemoryCleanupDecision(
            shouldClearGoalEnemy,
            profileIdsToForget);
    }

    private static bool ShouldForgetEnemyMemory(FollowerEnemyMemoryState enemy)
    {
        if (enemy.IsProtected
            || enemy.IsVisible
            || enemy.CanShoot)
        {
            return false;
        }

        return enemy.LastSeenAgeSeconds < 0f
            || enemy.LastSeenAgeSeconds >= DefaultStaleEnemyLastSeenThresholdSeconds;
    }
}

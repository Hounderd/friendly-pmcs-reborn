namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerThreatMemoryRetentionPolicy
{
    public const float DefaultRecentThreatRetentionSeconds = 15f;
    public const float DefaultActionableThreatRetentionSeconds = 5f;

    public static bool ShouldRetainGoalEnemy(
        string? goalTargetProfileId,
        string? recentThreatAttackerProfileId,
        float recentThreatAgeSeconds)
    {
        if (string.IsNullOrWhiteSpace(goalTargetProfileId)
            || string.IsNullOrWhiteSpace(recentThreatAttackerProfileId)
            || recentThreatAgeSeconds < 0f
            || recentThreatAgeSeconds > DefaultRecentThreatRetentionSeconds)
        {
            return false;
        }

        return string.Equals(goalTargetProfileId, recentThreatAttackerProfileId, StringComparison.Ordinal);
    }

    public static bool ShouldTreatGoalEnemyAsActionable(
        string? goalTargetProfileId,
        string? recentThreatAttackerProfileId,
        float recentThreatAgeSeconds)
    {
        if (string.IsNullOrWhiteSpace(goalTargetProfileId)
            || string.IsNullOrWhiteSpace(recentThreatAttackerProfileId)
            || recentThreatAgeSeconds < 0f
            || recentThreatAgeSeconds > DefaultActionableThreatRetentionSeconds)
        {
            return false;
        }

        return string.Equals(goalTargetProfileId, recentThreatAttackerProfileId, StringComparison.Ordinal);
    }
}

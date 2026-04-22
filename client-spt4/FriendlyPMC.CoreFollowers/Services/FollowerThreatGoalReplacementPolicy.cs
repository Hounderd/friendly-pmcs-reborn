namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerThreatGoalReplacementDecision(
    bool ShouldForceReplaceGoal);

public static class FollowerThreatGoalReplacementPolicy
{
    private const float StaleGoalLastSeenThresholdSeconds = 5f;

    public static FollowerThreatGoalReplacementDecision Evaluate(
        bool bootstrapSucceeded,
        string? currentTargetProfileId,
        bool currentTargetVisible,
        bool currentTargetCanShoot,
        float currentTargetLastSeenAgeSeconds,
        bool currentTargetIsProtected,
        string? incomingTargetProfileId)
    {
        if (!bootstrapSucceeded || string.IsNullOrWhiteSpace(incomingTargetProfileId))
        {
            return default;
        }

        if (string.IsNullOrWhiteSpace(currentTargetProfileId))
        {
            return new FollowerThreatGoalReplacementDecision(true);
        }

        if (string.Equals(currentTargetProfileId, incomingTargetProfileId, StringComparison.Ordinal))
        {
            return default;
        }

        if (currentTargetIsProtected)
        {
            return new FollowerThreatGoalReplacementDecision(true);
        }

        var currentTargetIsActionable = currentTargetVisible || currentTargetCanShoot;
        var currentTargetIsStale = !currentTargetIsActionable
            && (currentTargetLastSeenAgeSeconds < 0f || currentTargetLastSeenAgeSeconds >= StaleGoalLastSeenThresholdSeconds);

        return new FollowerThreatGoalReplacementDecision(currentTargetIsStale);
    }
}

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerUnderFireReaction(
    bool ShouldSetUnderFire,
    bool ShouldAttemptThreatBootstrap,
    bool ShouldPromoteAttackerAsGoalEnemy,
    bool ShouldBreakHealing);

public static class FollowerUnderFireReactionPolicy
{
    public static FollowerUnderFireReaction Evaluate(
        string? attackerProfileId,
        bool attackerIsProtected,
        string? currentTargetProfileId,
        bool currentTargetIsProtected)
    {
        if (string.IsNullOrWhiteSpace(attackerProfileId) || attackerIsProtected)
        {
            return default;
        }

        var shouldPromoteAttacker = string.IsNullOrWhiteSpace(currentTargetProfileId)
            || currentTargetIsProtected
            || string.Equals(currentTargetProfileId, attackerProfileId, StringComparison.Ordinal);

        return new FollowerUnderFireReaction(
            ShouldSetUnderFire: true,
            ShouldAttemptThreatBootstrap: true,
            ShouldPromoteAttackerAsGoalEnemy: shouldPromoteAttacker,
            ShouldBreakHealing: true);
    }
}

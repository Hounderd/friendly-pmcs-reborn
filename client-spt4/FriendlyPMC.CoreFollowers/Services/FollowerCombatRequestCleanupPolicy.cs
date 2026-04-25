using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerCombatRequestCleanupDecision(
    bool ShouldStopCurrentRequest);

public static class FollowerCombatRequestCleanupPolicy
{
    public static FollowerCombatRequestCleanupDecision Evaluate(
        FollowerCommand command,
        CustomFollowerBrainMode mode,
        string? currentRequestType,
        bool hasActionableEnemy,
        bool isInFollowCombatSuppressionCooldown)
    {
        if (IsSuppressionRequest(currentRequestType))
        {
            if (mode == CustomFollowerBrainMode.CombatPursue)
            {
                return default;
            }

            var shouldStopSuppression = command == FollowerCommand.Follow
                || isInFollowCombatSuppressionCooldown
                || !hasActionableEnemy;
            return new FollowerCombatRequestCleanupDecision(shouldStopSuppression);
        }

        if (IsTakeCoverRequest(currentRequestType))
        {
            var shouldStopCover = (command == FollowerCommand.Follow && isInFollowCombatSuppressionCooldown)
                || mode != CustomFollowerBrainMode.CombatPursue
                || !hasActionableEnemy;
            return new FollowerCombatRequestCleanupDecision(shouldStopCover);
        }

        if (!IsAttackCloseRequest(currentRequestType))
        {
            return default;
        }

        var shouldStop = (command == FollowerCommand.Follow && isInFollowCombatSuppressionCooldown)
            || mode != CustomFollowerBrainMode.CombatPursue
            || !hasActionableEnemy;
        return new FollowerCombatRequestCleanupDecision(shouldStop);
    }

    public static bool IsSuppressionRequest(string? currentRequestType)
    {
        return string.Equals(currentRequestType, "suppressionFire", StringComparison.Ordinal);
    }

    public static bool IsAttackCloseRequest(string? currentRequestType)
    {
        return string.Equals(currentRequestType, "attackClose", StringComparison.Ordinal);
    }

    public static bool IsTakeCoverRequest(string? currentRequestType)
    {
        return string.Equals(currentRequestType, "getInCover", StringComparison.Ordinal);
    }

    public static bool IsCombatAssistRequest(string? currentRequestType)
    {
        return IsSuppressionRequest(currentRequestType)
            || IsAttackCloseRequest(currentRequestType)
            || IsTakeCoverRequest(currentRequestType);
    }
}

using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerCombatStallState(
    bool InCombatStall,
    float StallStartTime);

public readonly record struct CustomFollowerCombatStallResult(
    bool ShouldBreakStall,
    CustomFollowerCombatStallState NextState);

public static class CustomFollowerCombatStallPolicy
{
    private const float MinimumBreakDistanceMeters = 12f;
    private const float StallWindowSeconds = 1.25f;

    public static CustomFollowerCombatStallResult Evaluate(
        float now,
        CustomFollowerCombatStallState state,
        FollowerCommand command,
        CustomFollowerNavigationIntent navigationIntent,
        string? activeLayerName,
        string? activeLogicName,
        bool isMoving,
        float distanceToPlayerMeters)
    {
        var isRelevant = command == FollowerCommand.Follow
            && navigationIntent is CustomFollowerNavigationIntent.CatchUpToPlayer or CustomFollowerNavigationIntent.MoveToFormation
            && distanceToPlayerMeters >= MinimumBreakDistanceMeters
            && FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName)
            && IsStallLogic(activeLogicName)
            && !isMoving;

        if (!isRelevant)
        {
            return new CustomFollowerCombatStallResult(false, default);
        }

        if (!state.InCombatStall)
        {
            return new CustomFollowerCombatStallResult(
                false,
                new CustomFollowerCombatStallState(true, now));
        }

        var shouldBreak = now - state.StallStartTime >= StallWindowSeconds;
        return new CustomFollowerCombatStallResult(
            shouldBreak,
            new CustomFollowerCombatStallState(true, state.StallStartTime));
    }

    private static bool IsStallLogic(string? activeLogicName)
    {
        return string.Equals(activeLogicName, "FreezeAction", StringComparison.OrdinalIgnoreCase)
            || string.Equals(activeLogicName, "SeekCoverAction", StringComparison.OrdinalIgnoreCase)
            || string.Equals(activeLogicName, "SearchAction", StringComparison.OrdinalIgnoreCase);
    }
}

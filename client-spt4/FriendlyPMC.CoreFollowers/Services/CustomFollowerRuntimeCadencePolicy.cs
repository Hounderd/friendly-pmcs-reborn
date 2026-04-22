using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerRuntimeCadenceSettings(
    float DecisionReviewIntervalSeconds = 0.25f,
    float PathRefreshIntervalSeconds = 0.75f,
    float RecoveryRefreshIntervalSeconds = 0.5f);

public readonly record struct CustomFollowerRuntimeCadenceState(
    float NextDecisionReviewTime,
    float NextPathRefreshTime,
    FollowerCommand? LastCommand,
    bool LastPreferredTarget,
    bool LastNavigationStuck,
    bool LastActionableEnemy,
    bool LastUnderFire);

public readonly record struct CustomFollowerRuntimeCadenceResult(
    bool ShouldReviewDecision,
    bool ShouldRefreshPath,
    CustomFollowerRuntimeCadenceState NextState);

public static class CustomFollowerRuntimeCadencePolicy
{
    public static CustomFollowerRuntimeCadenceResult Evaluate(
        float now,
        CustomFollowerRuntimeCadenceState state,
        FollowerCommand currentCommand,
        bool hasActionableEnemy,
        bool hasPreferredTarget,
        bool isUnderFire,
        bool isNavigationStuck,
        CustomFollowerRuntimeCadenceSettings settings = default)
    {
        var commandChanged = state.LastCommand != currentCommand;
        var preferredTargetChanged = state.LastPreferredTarget != hasPreferredTarget;
        var stuckTransitioned = state.LastNavigationStuck != isNavigationStuck;
        var actionableEnemyChanged = state.LastActionableEnemy != hasActionableEnemy;
        var underFireChanged = state.LastUnderFire != isUnderFire;
        var threatStateChanged = actionableEnemyChanged || underFireChanged;

        var shouldReviewDecision = state.NextDecisionReviewTime <= 0f
            || now >= state.NextDecisionReviewTime
            || commandChanged
            || preferredTargetChanged
            || stuckTransitioned
            || threatStateChanged;

        var shouldRefreshPath = state.NextPathRefreshTime <= 0f
            || now >= state.NextPathRefreshTime
            || commandChanged
            || stuckTransitioned
            || threatStateChanged;

        var nextDecisionReviewTime = shouldReviewDecision
            ? now + NormalizeInterval(settings.DecisionReviewIntervalSeconds, 0.25f)
            : state.NextDecisionReviewTime;
        var nextPathRefreshTime = shouldRefreshPath
            ? now + NormalizeInterval(
                isNavigationStuck
                    ? settings.RecoveryRefreshIntervalSeconds
                    : settings.PathRefreshIntervalSeconds,
                isNavigationStuck ? 0.5f : 0.75f)
            : state.NextPathRefreshTime;

        return new CustomFollowerRuntimeCadenceResult(
            shouldReviewDecision,
            shouldRefreshPath,
            new CustomFollowerRuntimeCadenceState(
                nextDecisionReviewTime,
                nextPathRefreshTime,
                currentCommand,
                hasPreferredTarget,
                isNavigationStuck,
                hasActionableEnemy,
                isUnderFire));
    }

    private static float NormalizeInterval(float value, float fallback)
    {
        return value > 0f ? value : fallback;
    }
}

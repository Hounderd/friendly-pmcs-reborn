namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerNavigationProgressState(
    CustomFollowerNavigationIntent LastIntent,
    BotDebugWorldPoint LastSamplePosition,
    float LastSampleTime,
    float LastDistanceToPlayerMeters,
    float NotMovingStartTime);

public readonly record struct CustomFollowerNavigationStuckResult(
    bool IsStuck,
    CustomFollowerNavigationProgressState NextState);

public static class CustomFollowerNavigationStuckPolicy
{
    private const float MinimumTrackedDistanceMeters = 6f;
    private const float MinimumProgressMeters = 0.75f;
    private const float MinimumClosingProgressMeters = 0.35f;
    private const float StagnationWindowSeconds = 1.25f;
    private const float NotMovingStagnationWindowSeconds = 2.0f;

    public static CustomFollowerNavigationStuckResult Evaluate(
        float now,
        CustomFollowerNavigationProgressState state,
        CustomFollowerNavigationIntent navigationIntent,
        BotDebugWorldPoint currentPosition,
        float distanceToPlayerMeters,
        bool isMoving)
    {
        if (!RequiresMovementTracking(navigationIntent) || distanceToPlayerMeters < MinimumTrackedDistanceMeters)
        {
            return new CustomFollowerNavigationStuckResult(
                false,
                new CustomFollowerNavigationProgressState(
                    navigationIntent,
                    currentPosition,
                    now,
                    distanceToPlayerMeters,
                    NotMovingStartTime: 0f));
        }

        if (state.LastSampleTime <= 0f || state.LastIntent != navigationIntent)
        {
            return new CustomFollowerNavigationStuckResult(
                false,
                new CustomFollowerNavigationProgressState(
                    navigationIntent,
                    currentPosition,
                    now,
                    distanceToPlayerMeters,
                    NotMovingStartTime: isMoving ? 0f : now));
        }

        if (!isMoving)
        {
            var notMovingStart = state.NotMovingStartTime > 0f ? state.NotMovingStartTime : now;
            var isNotMovingStuck = now - notMovingStart >= NotMovingStagnationWindowSeconds;
            return new CustomFollowerNavigationStuckResult(
                isNotMovingStuck,
                new CustomFollowerNavigationProgressState(
                    navigationIntent,
                    currentPosition,
                    state.LastSampleTime,
                    state.LastDistanceToPlayerMeters,
                    NotMovingStartTime: notMovingStart));
        }

        if (now - state.LastSampleTime < StagnationWindowSeconds)
        {
            return new CustomFollowerNavigationStuckResult(
                false,
                state with { NotMovingStartTime = 0f });
        }

        var movedDistance = state.LastSamplePosition.DistanceTo(currentPosition);
        var closingProgress = state.LastDistanceToPlayerMeters - distanceToPlayerMeters;
        var requiresClosingProgress = RequiresClosingProgress(navigationIntent);
        var isStuck = movedDistance < MinimumProgressMeters
            || (requiresClosingProgress && closingProgress < MinimumClosingProgressMeters);
        var nextState = new CustomFollowerNavigationProgressState(
            navigationIntent,
            currentPosition,
            now,
            distanceToPlayerMeters,
            NotMovingStartTime: 0f);

        return new CustomFollowerNavigationStuckResult(isStuck, nextState);
    }

    private static bool RequiresMovementTracking(CustomFollowerNavigationIntent navigationIntent)
    {
        return navigationIntent is CustomFollowerNavigationIntent.MoveToFormation
            or CustomFollowerNavigationIntent.CatchUpToPlayer
            or CustomFollowerNavigationIntent.ReturnToAnchor
            or CustomFollowerNavigationIntent.ReturnToCombatRange
            or CustomFollowerNavigationIntent.RepathAndRecover;
    }

    private static bool RequiresClosingProgress(CustomFollowerNavigationIntent navigationIntent)
    {
        return navigationIntent is CustomFollowerNavigationIntent.MoveToFormation
            or CustomFollowerNavigationIntent.CatchUpToPlayer
            or CustomFollowerNavigationIntent.ReturnToCombatRange
            or CustomFollowerNavigationIntent.RepathAndRecover;
    }
}

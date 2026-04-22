namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerMovementDispatchState(
    CustomFollowerNavigationIntent LastIntent,
    BotDebugWorldPoint LastTargetPoint,
    float LastDispatchTime,
    float LastDistanceToPlayerMeters);

public readonly record struct CustomFollowerMovementDispatchResult(
    bool ShouldDispatch,
    CustomFollowerMovementDispatchState NextState);

public static class CustomFollowerMovementDispatchPolicy
{
    private const float MinimumTargetShiftMeters = 0.9f;
    private const float RedispatchIntervalSeconds = 1.25f;
    private const float AggressiveCatchUpDistanceMeters = 30f;
    private const float AggressiveCatchUpRedispatchIntervalSeconds = 1f;
    private const float WorseningDistanceThresholdMeters = 1.5f;

    public static CustomFollowerMovementDispatchResult Evaluate(
        float now,
        CustomFollowerMovementDispatchState state,
        CustomFollowerNavigationIntent navigationIntent,
        BotDebugWorldPoint targetPoint,
        float distanceToPlayerMeters)
    {
        if (navigationIntent == CustomFollowerNavigationIntent.None)
        {
            return new CustomFollowerMovementDispatchResult(
                false,
                new CustomFollowerMovementDispatchState(
                    navigationIntent,
                    targetPoint,
                    state.LastDispatchTime,
                    distanceToPlayerMeters));
        }

        var aggressiveCatchUp = RequiresAggressiveCatchUp(navigationIntent, distanceToPlayerMeters);
        var redispatchInterval = aggressiveCatchUp
            ? AggressiveCatchUpRedispatchIntervalSeconds
            : RedispatchIntervalSeconds;
        var distanceGotWorse = aggressiveCatchUp
            && state.LastDistanceToPlayerMeters > 0f
            && distanceToPlayerMeters - state.LastDistanceToPlayerMeters >= WorseningDistanceThresholdMeters;
        var shouldDispatch = state.LastDispatchTime <= 0f
            || state.LastIntent != navigationIntent
            || state.LastTargetPoint.DistanceTo(targetPoint) >= MinimumTargetShiftMeters
            || now - state.LastDispatchTime >= redispatchInterval
            || distanceGotWorse;

        var nextState = shouldDispatch
            ? new CustomFollowerMovementDispatchState(
                navigationIntent,
                targetPoint,
                now,
                distanceToPlayerMeters)
            : state;

        return new CustomFollowerMovementDispatchResult(shouldDispatch, nextState);
    }

    private static bool RequiresAggressiveCatchUp(
        CustomFollowerNavigationIntent navigationIntent,
        float distanceToPlayerMeters)
    {
        return distanceToPlayerMeters >= AggressiveCatchUpDistanceMeters
            && navigationIntent is CustomFollowerNavigationIntent.CatchUpToPlayer
                or CustomFollowerNavigationIntent.RepathAndRecover
                or CustomFollowerNavigationIntent.ReturnToCombatRange;
    }
}

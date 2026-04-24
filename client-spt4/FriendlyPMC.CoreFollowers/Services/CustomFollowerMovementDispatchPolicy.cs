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
    private const float StableFormationRedispatchSuppressionDistanceMeters = 12f;
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
        var isInitialDispatch = state.LastDispatchTime <= 0f;
        var suppressStableFormationRedispatch = !isInitialDispatch
            && state.LastIntent == navigationIntent
            && navigationIntent == CustomFollowerNavigationIntent.MoveToFormation
            && distanceToPlayerMeters <= StableFormationRedispatchSuppressionDistanceMeters;
        var targetShiftIsMaterial = state.LastTargetPoint.DistanceTo(targetPoint) >= MinimumTargetShiftMeters;
        var cadenceElapsed = now - state.LastDispatchTime >= redispatchInterval;
        var shouldDispatch = isInitialDispatch
            || state.LastIntent != navigationIntent
            || targetShiftIsMaterial
            || (!suppressStableFormationRedispatch && cadenceElapsed)
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

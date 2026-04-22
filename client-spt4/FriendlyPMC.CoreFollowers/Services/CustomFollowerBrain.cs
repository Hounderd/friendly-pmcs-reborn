using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed class CustomFollowerBrain
{
    private readonly CustomFollowerReceiver receiver;
    private float lastCombatPressureTimestampSeconds = -1f;

    public CustomFollowerBrain(CustomFollowerReceiver receiver)
    {
        this.receiver = receiver;
        CurrentDebugState = new CustomFollowerDebugState(
            receiver.CurrentState.Command,
            CustomFollowerBrainMode.FollowFormation,
            CustomFollowerNavigationIntent.None,
            PreferPlayerTarget: false);
    }

    public CustomFollowerDebugState CurrentDebugState { get; private set; }

    public CustomFollowerBrainDecision Evaluate(
        float distanceToPlayerMeters,
        float distanceToHoldAnchorMeters,
        bool hasActionableEnemy,
        float distanceToNearestActionableEnemyMeters,
        float distanceToGoalEnemyMeters,
        bool isUnderFire,
        bool goalEnemyHaveSeen,
        float goalEnemyLastSeenAgeSeconds,
        bool hasPreferredTarget,
        bool isNavigationStuck,
        bool isInFollowCombatSuppressionCooldown,
        FollowerModeSettings settings)
    {
        var now = GetCurrentTime();
        var goalEnemySeenRecently = goalEnemyHaveSeen
            && goalEnemyLastSeenAgeSeconds >= 0f
            && goalEnemyLastSeenAgeSeconds <= CustomFollowerBrainPolicy.GoalEnemySeenRecentSeconds
            && distanceToGoalEnemyMeters < float.MaxValue;
        var hasConfirmedCombatPressure = hasActionableEnemy
            || isUnderFire
            || goalEnemySeenRecently;
        if (hasConfirmedCombatPressure)
        {
            lastCombatPressureTimestampSeconds = now;
        }

        var hasRecentCombatPressure = lastCombatPressureTimestampSeconds > 0f
            && now - lastCombatPressureTimestampSeconds <= CustomFollowerBrainPolicy.CombatPressureStickinessSeconds;
        var decision = CustomFollowerBrainPolicy.Evaluate(
            new CustomFollowerBrainContext(
                receiver.CurrentState.Command,
                CurrentDebugState.Mode,
                distanceToPlayerMeters,
                distanceToHoldAnchorMeters,
                hasActionableEnemy,
                distanceToNearestActionableEnemyMeters,
                distanceToGoalEnemyMeters,
                isUnderFire,
                hasPreferredTarget,
                hasRecentCombatPressure,
                isNavigationStuck,
                isInFollowCombatSuppressionCooldown,
                settings));

        CurrentDebugState = CurrentDebugState with
        {
            Command = receiver.CurrentState.Command,
            Mode = decision.Mode,
            PreferPlayerTarget = decision.PreferPlayerTarget,
        };

        return decision;
    }

    private static float GetCurrentTime()
    {
#if SPT_CLIENT
        return UnityEngine.Time.time;
#else
        return CustomFollowerReceiver.TimeProvider();
#endif
    }
}

using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed class CustomFollowerBrainRuntimeSession
{
    private CustomFollowerRuntimeCadenceState cadenceState;

    public CustomFollowerBrainRuntimeSession()
    {
        Receiver = new CustomFollowerReceiver();
        Brain = new CustomFollowerBrain(Receiver);
        Navigation = new CustomFollowerNavigationController();
        Combat = new CustomFollowerCombatController();
        CurrentDebugState = Brain.CurrentDebugState;
    }

    public CustomFollowerReceiver Receiver { get; }

    public CustomFollowerBrain Brain { get; }

    public CustomFollowerNavigationController Navigation { get; }

    public CustomFollowerCombatController Combat { get; }

    public CustomFollowerDebugState CurrentDebugState { get; private set; }

    public CustomFollowerRuntimeCadenceState CadenceState => cadenceState;

    public void ApplyCommand(FollowerCommand command, BotDebugWorldPoint currentPosition)
    {
        if (command is FollowerCommand.Hold or FollowerCommand.TakeCover)
        {
            Receiver.SetHoldAnchor(currentPosition);
            if (command == FollowerCommand.TakeCover)
            {
                Receiver.SetCommand(command);
            }
            return;
        }

        Receiver.SetCommand(command);
    }

    public CustomFollowerDebugState Evaluate(
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
        FollowerModeSettings settings)
    {
        var decision = Brain.Evaluate(
            distanceToPlayerMeters,
            distanceToHoldAnchorMeters,
            hasActionableEnemy,
            distanceToNearestActionableEnemyMeters,
            distanceToGoalEnemyMeters,
            isUnderFire,
            goalEnemyHaveSeen,
            goalEnemyLastSeenAgeSeconds,
            hasPreferredTarget,
            isNavigationStuck,
            Receiver.IsInFollowCombatSuppressionCooldown(GetCurrentTime()),
            settings);
        var navigationIntent = Navigation.Update(decision);
        CurrentDebugState = Brain.CurrentDebugState with
        {
            NavigationIntent = navigationIntent,
        };
        return CurrentDebugState;
    }

    public CustomFollowerBrainTickResult Tick(
        float now,
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
        FollowerModeSettings settings,
        CustomFollowerRuntimeCadenceSettings cadenceSettings = default)
    {
        var cadenceResult = CustomFollowerRuntimeCadencePolicy.Evaluate(
            now,
            cadenceState,
            Receiver.CurrentState.Command,
            hasActionableEnemy,
            hasPreferredTarget,
            isUnderFire,
            isNavigationStuck,
            cadenceSettings);
        cadenceState = cadenceResult.NextState;

        if (cadenceResult.ShouldReviewDecision)
        {
            Evaluate(
                distanceToPlayerMeters,
                distanceToHoldAnchorMeters,
                hasActionableEnemy,
                distanceToNearestActionableEnemyMeters,
                distanceToGoalEnemyMeters,
                isUnderFire,
                goalEnemyHaveSeen,
                goalEnemyLastSeenAgeSeconds,
                hasPreferredTarget,
                isNavigationStuck,
                settings);
        }

        return new CustomFollowerBrainTickResult(
            cadenceResult.ShouldReviewDecision,
            cadenceResult.ShouldRefreshPath,
            CurrentDebugState);
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

public readonly record struct CustomFollowerBrainTickResult(
    bool ReviewedDecision,
    bool RefreshedPath,
    CustomFollowerDebugState DebugState);

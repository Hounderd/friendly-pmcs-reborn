using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FriendlyFollowerMovementControlDecision(
    bool ShouldControlMovement,
    bool YieldedToCombatPressure);

public static class FriendlyFollowerMovementControlPolicy
{
    private const float CombatReturnImmediateThreatDistanceMeters = 30f;
    private const float FollowCatchUpImmediateThreatDistanceMeters = 12f;

    public static FriendlyFollowerMovementControlDecision Evaluate(
        DebugSpawnFollowerControlPath? controlPath,
        FollowerCommand command,
        CustomFollowerBrainMode? customBrainMode,
        bool hasActionableEnemy,
        float distanceToNearestActionableEnemyMeters,
        bool isUnderFire,
        bool legacyShouldControlMovement)
    {
        if (command == FollowerCommand.Loot)
        {
            return new FriendlyFollowerMovementControlDecision(
                ShouldControlMovement: false,
                YieldedToCombatPressure: false);
        }

        if (controlPath == DebugSpawnFollowerControlPath.CustomBrain && customBrainMode.HasValue)
        {
            if (!FriendlyFollowerMovementLayerActivationPolicy.ShouldActivate(controlPath, customBrainMode))
            {
                return new FriendlyFollowerMovementControlDecision(
                    ShouldControlMovement: false,
                    YieldedToCombatPressure: false);
            }

            if (ShouldYieldToCombatPressure(
                    command,
                    customBrainMode.Value,
                    hasActionableEnemy,
                    distanceToNearestActionableEnemyMeters,
                    isUnderFire))
            {
                return new FriendlyFollowerMovementControlDecision(
                    ShouldControlMovement: false,
                    YieldedToCombatPressure: true);
            }

            return new FriendlyFollowerMovementControlDecision(
                ShouldControlMovement: true,
                YieldedToCombatPressure: false);
        }

        return new FriendlyFollowerMovementControlDecision(
            ShouldControlMovement: legacyShouldControlMovement,
            YieldedToCombatPressure: false);
    }

    private static bool ShouldYieldToCombatPressure(
        FollowerCommand command,
        CustomFollowerBrainMode customBrainMode,
        bool hasActionableEnemy,
        float distanceToNearestActionableEnemyMeters,
        bool isUnderFire)
    {
        if (isUnderFire)
        {
            return true;
        }

        if (!hasActionableEnemy)
        {
            return false;
        }

        return command switch
        {
            FollowerCommand.Follow or FollowerCommand.Regroup
                when customBrainMode == CustomFollowerBrainMode.FollowCatchUp
                => distanceToNearestActionableEnemyMeters <= FollowCatchUpImmediateThreatDistanceMeters,
            FollowerCommand.Follow or FollowerCommand.Regroup => true,
            FollowerCommand.Combat when customBrainMode == CustomFollowerBrainMode.CombatReturnToRange
                => distanceToNearestActionableEnemyMeters <= CombatReturnImmediateThreatDistanceMeters,
            _ => false,
        };
    }
}

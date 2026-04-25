using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class CustomFollowerBrainPolicy
{
    public const float FollowCombatEngagementRangeMeters = 80f;
    public const float FollowCombatSuppressionCooldownSeconds = 20f;
    public const float FollowImmediateDefenseRangeMeters = 12f;
    public const float CombatPressureStickinessSeconds = 3f;
    public const float CombatPressureStickinessRangeMeters = 45f;
    public const float GoalEnemySeenRecentSeconds = 4f;

    public static CustomFollowerBrainDecision Evaluate(CustomFollowerBrainContext context)
    {
        if (context.IsNavigationStuck)
        {
            return new CustomFollowerBrainDecision(CustomFollowerBrainMode.RecoverNavigation, context.HasPreferredTarget);
        }

        return context.Command switch
        {
            FollowerCommand.Follow => EvaluateFollow(context),
            FollowerCommand.Hold => EvaluateHold(context),
            FollowerCommand.TakeCover => EvaluateHold(context),
            FollowerCommand.Combat => EvaluateCombat(context),
            FollowerCommand.Regroup => EvaluateFollow(context),
            _ => throw new ArgumentOutOfRangeException(nameof(context), context.Command, null),
        };
    }

    private static CustomFollowerBrainDecision EvaluateFollow(CustomFollowerBrainContext context)
    {
        var combatPressureDistance = ResolveCombatPressureDistance(context);
        var shouldEscalateToCombat = context.HasActionableEnemy
            && ShouldAllowFollowCombat(context, combatPressureDistance);
        var shouldMaintainCombat = context.CurrentMode == CustomFollowerBrainMode.CombatPursue
            && context.HasRecentCombatPressure
            && ShouldAllowFollowCombat(context, combatPressureDistance)
            && combatPressureDistance <= CombatPressureStickinessRangeMeters
            && (!context.IsInFollowCombatSuppressionCooldown
                || combatPressureDistance <= FollowImmediateDefenseRangeMeters);
        if (shouldEscalateToCombat || shouldMaintainCombat)
        {
            return new CustomFollowerBrainDecision(CustomFollowerBrainMode.CombatPursue, context.HasPreferredTarget);
        }

        var mode = context.DistanceToPlayerMeters >= context.Settings.EffectiveCatchUpDistanceMeters
            ? CustomFollowerBrainMode.FollowCatchUp
            : CustomFollowerBrainMode.FollowFormation;

        return new CustomFollowerBrainDecision(mode, context.HasPreferredTarget);
    }

    private static CustomFollowerBrainDecision EvaluateHold(CustomFollowerBrainContext context)
    {
        var mode = context.DistanceToHoldAnchorMeters > context.Settings.HoldRadiusMeters
            ? CustomFollowerBrainMode.HoldReturnToAnchor
            : CustomFollowerBrainMode.HoldDefendLocal;

        return new CustomFollowerBrainDecision(mode, context.HasPreferredTarget);
    }

    private static CustomFollowerBrainDecision EvaluateCombat(CustomFollowerBrainContext context)
    {
        var mode = context.DistanceToPlayerMeters > context.Settings.CombatMaxRangeMeters
            ? CustomFollowerBrainMode.CombatReturnToRange
            : CustomFollowerBrainMode.CombatPursue;

        return new CustomFollowerBrainDecision(mode, context.HasPreferredTarget);
    }

    private static float ResolveCombatPressureDistance(CustomFollowerBrainContext context)
    {
        var nearestActionable = context.DistanceToNearestActionableEnemyMeters;
        var goalEnemy = context.DistanceToGoalEnemyMeters;

        if (nearestActionable < float.MaxValue)
        {
            return goalEnemy < float.MaxValue
                ? MathF.Min(nearestActionable, goalEnemy)
                : nearestActionable;
        }

        return goalEnemy;
    }

    private static bool ShouldAllowFollowCombat(CustomFollowerBrainContext context, float combatPressureDistance)
    {
        if (context.IsUnderFire)
        {
            return context.DistanceToPlayerMeters <= context.Settings.CombatMaxRangeMeters;
        }

        if (combatPressureDistance <= FollowImmediateDefenseRangeMeters)
        {
            return true;
        }

        return context.DistanceToPlayerMeters <= context.Settings.FollowLeashDistanceMeters;
    }
}

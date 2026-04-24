using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerCombatEngagementUnblockDecision(
    bool ShouldInterruptHealing,
    bool ShouldStopCombatAssistRequest,
    bool ShouldReclaimFromLooting);

public static class FollowerCombatEngagementUnblockPolicy
{
    public const float DefaultVisibleThreatBreakDistanceMeters = 18f;
    public const float DefaultUnderFireThreatBreakDistanceMeters = 35f;
    public const float DefaultAttackCloseStallBreakAgeSeconds = 2.5f;
    public const float DefaultTakeCoverStallBreakAgeSeconds = 3.5f;

    public static FollowerCombatEngagementUnblockDecision Evaluate(
        FollowerCommand command,
        CustomFollowerBrainMode mode,
        string? activeLayerName,
        string? currentRequestType,
        bool hasActionableEnemy,
        bool targetVisible,
        bool canShoot,
        bool isUnderFire,
        float distanceToNearestActionableEnemyMeters,
        bool isHealing,
        float currentRequestAgeSeconds,
        bool isMoving)
    {
        if (command is not (FollowerCommand.Follow or FollowerCommand.Combat or FollowerCommand.TakeCover)
            || mode != CustomFollowerBrainMode.CombatPursue
            || FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName))
        {
            return default;
        }

        var hasImmediateCombatPressure = hasActionableEnemy
            && ((targetVisible && distanceToNearestActionableEnemyMeters <= DefaultVisibleThreatBreakDistanceMeters)
                || (canShoot && distanceToNearestActionableEnemyMeters <= FollowerSuppressionEngagementPolicy.DefaultShootableThreatSuppressionBreakDistanceMeters)
                || (isUnderFire && distanceToNearestActionableEnemyMeters <= DefaultUnderFireThreatBreakDistanceMeters));
        var hasVisibleUnshootableThreat = hasActionableEnemy
            && targetVisible
            && !canShoot;
        if (!hasImmediateCombatPressure && !hasVisibleUnshootableThreat)
        {
            return default;
        }

        var shouldStopCombatAssistRequest = FollowerCombatRequestCleanupPolicy.IsSuppressionRequest(currentRequestType)
            || (FollowerCombatRequestCleanupPolicy.IsAttackCloseRequest(currentRequestType)
                && targetVisible
                && canShoot
                && !isMoving
                && currentRequestAgeSeconds >= DefaultAttackCloseStallBreakAgeSeconds)
            || (FollowerCombatRequestCleanupPolicy.IsTakeCoverRequest(currentRequestType)
                && hasVisibleUnshootableThreat
                && !isMoving
                && currentRequestAgeSeconds >= DefaultTakeCoverStallBreakAgeSeconds);

        return new FollowerCombatEngagementUnblockDecision(
            ShouldInterruptHealing: isHealing,
            ShouldStopCombatAssistRequest: shouldStopCombatAssistRequest,
            ShouldReclaimFromLooting: FollowerCombatLayerPolicy.IsLootingLayer(activeLayerName));
    }
}

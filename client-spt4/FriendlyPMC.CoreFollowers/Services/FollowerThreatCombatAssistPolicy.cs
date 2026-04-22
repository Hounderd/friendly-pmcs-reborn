using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerThreatCombatAssistDecision(
    bool ShouldActivateSuppression,
    bool ShouldActivateAttackClose,
    bool ShouldActivateTakeCover);

public static class FollowerThreatCombatAssistPolicy
{
    public static FollowerThreatCombatAssistDecision Evaluate(
        bool bootstrapSucceeded,
        FollowerCommand command,
        string? activeLayerName,
        string? currentRequestType,
        bool isInFollowCombatSuppressionCooldown,
        bool targetVisible,
        bool canShoot,
        float distanceToNearestActionableEnemyMeters)
    {
        var shouldUseSuppression = FollowerSuppressionEngagementPolicy.ShouldUseSuppression(
            targetVisible,
            canShoot,
            distanceToNearestActionableEnemyMeters);
        var shouldUseTakeCover = targetVisible
            && !canShoot
            && distanceToNearestActionableEnemyMeters > FollowerSuppressionEngagementPolicy.DefaultVisibleThreatSuppressionBreakDistanceMeters;
        var isActivationBlocked = FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName)
            || FollowerCombatRequestCleanupPolicy.IsCombatAssistRequest(currentRequestType)
            || (command == FollowerCommand.Follow && isInFollowCombatSuppressionCooldown);
        var shouldActivateSuppression = bootstrapSucceeded
            && command is FollowerCommand.Follow or FollowerCommand.Combat or FollowerCommand.TakeCover
            && shouldUseSuppression
            && !shouldUseTakeCover
            && !isActivationBlocked;
        var shouldActivateAttackClose = bootstrapSucceeded
            && command is FollowerCommand.Follow or FollowerCommand.Combat or FollowerCommand.TakeCover
            && !shouldUseTakeCover
            && !shouldUseSuppression
            && !isActivationBlocked;
        var shouldActivateTakeCover = bootstrapSucceeded
            && command is FollowerCommand.Follow or FollowerCommand.Combat or FollowerCommand.TakeCover
            && shouldUseTakeCover
            && !isActivationBlocked;

        return new FollowerThreatCombatAssistDecision(
            shouldActivateSuppression,
            shouldActivateAttackClose,
            shouldActivateTakeCover);
    }
}

using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerCombatAssistState(
    string? LastTargetProfileId,
    float LastActivationTimeSeconds);

public readonly record struct CustomFollowerCombatAssistResult(
    bool ShouldActivateSuppression,
    bool ShouldActivateAttackClose,
    bool ShouldActivateTakeCover,
    CustomFollowerCombatAssistState NextState);

public static class CustomFollowerCombatAssistPolicy
{
    private const float ActivationCooldownSeconds = 2.5f;

    public static CustomFollowerCombatAssistResult Evaluate(
        float now,
        CustomFollowerCombatAssistState state,
        FollowerCommand command,
        CustomFollowerBrainMode mode,
        bool isInFollowCombatSuppressionCooldown,
        string? activeLayerName,
        string? currentRequestType,
        bool haveEnemy,
        bool targetVisible,
        bool canShoot,
        string? targetProfileId,
        float distanceToNearestActionableEnemyMeters)
    {
        var hasCombatSignal = haveEnemy || !string.IsNullOrWhiteSpace(targetProfileId);
        var shouldUseBlindAttackClose = FollowerSuppressionEngagementPolicy.ShouldUseAttackCloseWithoutSight(
            targetVisible,
            canShoot,
            distanceToNearestActionableEnemyMeters);
        var shouldUseSuppression = FollowerSuppressionEngagementPolicy.ShouldUseSuppression(
            targetVisible,
            canShoot,
            distanceToNearestActionableEnemyMeters);
        var shouldUseTakeCover = false;
        var hasActiveCombatOwnership = FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName)
            || FollowerCombatRequestCleanupPolicy.IsCombatAssistRequest(currentRequestType);
        var isActivationBlocked = FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName)
            || FollowerCombatRequestCleanupPolicy.IsCombatAssistRequest(currentRequestType)
            || (command == FollowerCommand.Follow && isInFollowCombatSuppressionCooldown)
            || string.IsNullOrWhiteSpace(targetProfileId)
            || (state.LastActivationTimeSeconds > 0f
                && now - state.LastActivationTimeSeconds < ActivationCooldownSeconds
                && hasActiveCombatOwnership
                && string.Equals(state.LastTargetProfileId, targetProfileId, StringComparison.Ordinal));
        var shouldActivateSuppression = mode == CustomFollowerBrainMode.CombatPursue
            && command is FollowerCommand.Follow or FollowerCommand.Combat
            && hasCombatSignal
            && (targetVisible || canShoot)
            && shouldUseSuppression
            && !shouldUseBlindAttackClose
            && !shouldUseTakeCover
            && !isActivationBlocked;
        var shouldActivateAttackClose = mode == CustomFollowerBrainMode.CombatPursue
            && command is FollowerCommand.Follow or FollowerCommand.Combat
            && hasCombatSignal
            && (targetVisible || canShoot || shouldUseBlindAttackClose)
            && !shouldUseTakeCover
            && (!shouldUseSuppression || shouldUseBlindAttackClose)
            && !isActivationBlocked;
        var shouldActivateTakeCover = mode == CustomFollowerBrainMode.CombatPursue
            && command is FollowerCommand.Follow or FollowerCommand.Combat or FollowerCommand.TakeCover
            && hasCombatSignal
            && shouldUseTakeCover
            && !isActivationBlocked;

        var nextState = shouldActivateSuppression || shouldActivateAttackClose || shouldActivateTakeCover
            ? new CustomFollowerCombatAssistState(targetProfileId, now)
            : state;

        return new CustomFollowerCombatAssistResult(
            shouldActivateSuppression,
            shouldActivateAttackClose,
            shouldActivateTakeCover,
            nextState);
    }
}

#if SPT_CLIENT
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FriendlyFollowerMovementLogic : CustomLogic
{
    private FollowerMovementProgressSample? previousMovementSample;
    private FollowerMovementIntent? previousMovementIntent;
    private CustomFollowerMovementFrameState customMovementFrameState;

    public FriendlyFollowerMovementLogic(BotOwner botOwner)
        : base(botOwner)
    {
    }

    public override void Update(CustomLayer.ActionData data)
    {
        var plugin = FriendlyPmcCoreFollowersPlugin.Instance;
        var requester = GamePlayerOwner.MyPlayer;
        if (plugin is null || requester is null || BotOwner is null || BotOwner.IsDead)
        {
            return;
        }

        if (plugin.Registry.TryGetControlPathRuntimeByProfileId(BotOwner.ProfileId, out var controlPathRuntime)
            && controlPathRuntime.ActivePath == DebugSpawnFollowerControlPath.CustomBrain
            && plugin.Registry.TryGetCustomBrainSessionByProfileId(BotOwner.ProfileId, out var customBrainSession))
        {
            var combatPressure = FollowerCombatPressureContextResolver.Resolve(
                BotOwner,
                requester.ProfileId,
                plugin.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId),
                BotDebugSnapshotMapper.GetWorldPoint(BotOwner),
                recentThreatAttackerProfileId: null,
                recentThreatAgeSeconds: -1f);
            var controlDecision = FriendlyFollowerMovementControlPolicy.Evaluate(
                controlPathRuntime.ActivePath,
                customBrainSession.CurrentDebugState.Command,
                customBrainSession.CurrentDebugState.Mode,
                combatPressure.HasActionableEnemy,
                combatPressure.DistanceToNearestActionableEnemyMeters,
                combatPressure.IsUnderFire,
                legacyShouldControlMovement: false);
            if (!controlDecision.ShouldControlMovement)
            {
                return;
            }

            var customDistanceToPlayer = BotDebugSnapshotMapper.GetWorldPoint(BotOwner)
                .DistanceTo(BotDebugSnapshotMapper.GetWorldPoint(requester));
            var plan = CustomFollowerMovementExecutionPolicy.Resolve(
                customBrainSession.CurrentDebugState.Command,
                customBrainSession.CurrentDebugState.NavigationIntent,
                customDistanceToPlayer,
                plugin.ModeSettings);
            var formationSlotIndex = ResolveFormationSlotIndex(plugin, BotOwner.ProfileId);
            var desiredTargetPoint = CustomFollowerMovementTargetPointPolicy.Resolve(
                requester,
                customBrainSession.CurrentDebugState.Command,
                plan.MovementIntent,
                formationSlotIndex);
            var frameDecision = CustomFollowerMovementFramePolicy.Evaluate(
                Time.time,
                customMovementFrameState,
                plan,
                customBrainSession.CurrentDebugState.NavigationIntent,
                BotDebugSnapshotMapper.GetWorldPoint(requester),
                desiredTargetPoint,
                customDistanceToPlayer,
                BotOwner.Mover?.IsMoving ?? false);
            customMovementFrameState = frameDecision.NextState;

            switch (frameDecision.Action)
            {
                case CustomFollowerMovementFrameAction.Stop:
                    CustomFollowerMovementExecutor.TryExecutePlan(
                        BotOwner,
                        requester,
                        plan,
                        frameDecision.TargetPoint);
                    break;
                case CustomFollowerMovementFrameAction.IssuePathCommand:
                    CustomFollowerMovementExecutor.TryExecutePlan(
                        BotOwner,
                        requester,
                        plan,
                        frameDecision.TargetPoint);
                    break;
                case CustomFollowerMovementFrameAction.MaintainMotion:
                    CustomFollowerMovementExecutor.MaintainMotionState(BotOwner, plan);
                    break;
            }
            return;
        }

        if (!plugin.Registry.TryGetActiveOrderByProfileId(BotOwner.ProfileId, out var command)
            || !plugin.Registry.TryGetRuntimeByProfileId(BotOwner.ProfileId, out var runtimeFollower))
        {
            return;
        }

        if (plugin.Registry.TryGetControlPathRuntimeByProfileId(BotOwner.ProfileId, out var legacyControlPathRuntime)
            && !FriendlyFollowerMovementLayerActivationPolicy.ShouldActivate(legacyControlPathRuntime.ActivePath, customBrainMode: null))
        {
            return;
        }

        var targetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(BotOwner.Memory?.GoalEnemy);
        var haveProtectedTarget = FollowerProtectionPolicy.ShouldProtectPlayer(
            BotOwner.ProfileId,
            plugin.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId),
            requester.ProfileId,
            targetProfileId);
        var distanceToPlayer = runtimeFollower.CurrentPosition.DistanceTo(BotDebugSnapshotMapper.GetWorldPoint(requester));
        var distanceToHoldAnchor = plugin.Registry.TryGetHoldAnchor(runtimeFollower.Aid, out var holdAnchor)
            ? runtimeFollower.CurrentPosition.DistanceTo(holdAnchor)
            : 0f;
        var settings = plugin.ModeSettings;

        var disposition = FollowerModePolicy.ResolveDisposition(
            command,
            FollowerOrderLayerPolicy.HasActionableEnemy(BotOwner.Memory?.HaveEnemy == true, haveProtectedTarget),
            distanceToPlayer,
            distanceToHoldAnchor,
            settings);

        if (!FollowerRuntimeModeEnforcementPolicy.ShouldDriveMovement(command, disposition))
        {
            return;
        }

        var movementIntent = FollowerCatchUpPolicy.ResolveMovementIntent(command, distanceToPlayer, settings);
        if (movementIntent == FollowerMovementIntent.HoldFormation)
        {
            previousMovementIntent = movementIntent;
            previousMovementSample = new FollowerMovementProgressSample(UnityEngine.Time.time, distanceToPlayer);
            return;
        }

        var currentSample = new FollowerMovementProgressSample(UnityEngine.Time.time, distanceToPlayer);
        var forceRefresh = previousMovementIntent != movementIntent
            || (previousMovementSample.HasValue
                && FollowerUnstuckPolicy.ShouldRefreshMovement(
                    movementRequested: true,
                    previousMovementSample.Value,
                    currentSample));

        FollowerMovementStateApplier.TryDriveMovementOrder(
            BotOwner,
            requester,
            command,
            movementIntent,
            forceRefresh);

        previousMovementIntent = movementIntent;
        previousMovementSample = currentSample;
    }

    private static int ResolveFormationSlotIndex(FriendlyPmcCoreFollowersPlugin plugin, string profileId)
    {
        var orderedProfileIds = plugin.Registry.RuntimeFollowers
            .Where(follower => follower.IsOperational)
            .OrderBy(follower => follower.Aid, StringComparer.Ordinal)
            .Select(follower => follower.RuntimeProfileId)
            .ToArray();
        var index = Array.IndexOf(orderedProfileIds, profileId);

        return Math.Max(0, index);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FriendlyFollowerMovementLogic
{
}
#endif

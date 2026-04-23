#if SPT_CLIENT
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using Action = DrakiaXYZ.BigBrain.Brains.CustomLayer.Action;
using ActionData = DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FriendlyFollowerMovementLayer : CustomLayer
{
    public FriendlyFollowerMovementLayer(BotOwner botOwner, int priority)
        : base(botOwner, priority)
    {
    }

    public override string GetName()
    {
        return nameof(FriendlyFollowerMovementLayer);
    }

    public override Action GetNextAction()
    {
        return TryBuildAction(out var action)
            ? action
            : CurrentAction;
    }

    public override bool IsActive()
    {
        return ShouldControlMovement(out _);
    }

    public override bool IsCurrentActionEnding()
    {
        return !ShouldControlMovement(out _);
    }

    private bool TryBuildAction(out Action action)
    {
        action = CurrentAction;
        if (!ShouldControlMovement(out var command))
        {
            return false;
        }

        if (CurrentAction is not null && CurrentAction.Type == typeof(FriendlyFollowerMovementLogic))
        {
            action = CurrentAction;
            return true;
        }

        action = new Action(typeof(FriendlyFollowerMovementLogic), command.ToString(), new ActionData());
        CurrentAction = action;
        return true;
    }

    private bool ShouldControlMovement(out FollowerCommand command)
    {
        command = default;

        var plugin = FriendlyPmcCoreFollowersPlugin.Instance;
        if (plugin is null)
        {
            return false;
        }

        if (!plugin.Registry.TryGetActiveOrderByProfileId(BotOwner.ProfileId, out command))
        {
            return false;
        }

        var customBrainMode = plugin.Registry.TryGetCustomBrainSessionByProfileId(BotOwner.ProfileId, out var customBrainSession)
            ? customBrainSession.CurrentDebugState.Mode
            : (CustomFollowerBrainMode?)null;

        var controlPath = plugin.Registry.TryGetControlPathRuntimeByProfileId(BotOwner.ProfileId, out var controlPathRuntime)
            ? controlPathRuntime.ActivePath
            : (DebugSpawnFollowerControlPath?)null;

        if (!plugin.Registry.TryGetRuntimeByProfileId(BotOwner.ProfileId, out var runtimeFollower))
        {
            return false;
        }

        if (!runtimeFollower.IsOperational)
        {
            return false;
        }

        var requester = GamePlayerOwner.MyPlayer;
        if (requester is null)
        {
            return false;
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

        var legacyShouldControlMovement = FollowerOrderLayerPolicy.ShouldActivateMovementLayer(
            command,
            BotOwner.Memory?.HaveEnemy == true,
            haveProtectedTarget,
            distanceToPlayer,
            distanceToHoldAnchor,
            plugin.ModeSettings);
        var combatPressure = FollowerCombatPressureContextResolver.Resolve(
            BotOwner,
            requester.ProfileId,
            plugin.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId),
            runtimeFollower.CurrentPosition,
            recentThreatAttackerProfileId: null,
            recentThreatAgeSeconds: -1f);

        return FriendlyFollowerMovementControlPolicy.Evaluate(
            controlPath,
            command,
            customBrainMode,
            combatPressure.HasActionableEnemy,
            combatPressure.DistanceToNearestActionableEnemyMeters,
            combatPressure.IsUnderFire,
            legacyShouldControlMovement)
            .ShouldControlMovement;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FriendlyFollowerMovementLayer
{
}
#endif

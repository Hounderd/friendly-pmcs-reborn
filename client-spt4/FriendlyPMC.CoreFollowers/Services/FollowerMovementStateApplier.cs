#if SPT_CLIENT
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerMovementStateApplier
{
    public static bool TrySeedMovementOrder(BotOwner owner, Player requester, FollowerCommand command)
    {
        return TryApply(owner, requester, command, FollowerMovementIntent.MoveToFormation, driveMovement: false, forceRefresh: false);
    }

    public static bool TryDriveMovementOrder(BotOwner owner, Player requester, FollowerCommand command)
    {
        return TryDriveMovementOrder(owner, requester, command, FollowerMovementIntent.MoveToFormation);
    }

    public static bool TryDriveMovementOrder(
        BotOwner owner,
        Player requester,
        FollowerCommand command,
        FollowerMovementIntent intent,
        bool forceRefresh = false)
    {
        return TryApply(owner, requester, command, intent, driveMovement: true, forceRefresh: forceRefresh);
    }

    public static bool TryDriveCatchUpAuthority(
        BotOwner owner,
        Player requester,
        bool forceRefresh = false)
    {
        if (owner is null || requester is null || owner.IsDead)
        {
            return false;
        }

        var plan = FollowerCatchUpAuthorityMotionPolicy.Resolve();
        var botFollower = owner.BotFollower;
        if (botFollower?.PatrolDataFollower is null)
        {
            botFollower?.Activate();
        }

        if (plan.ShouldPausePatrolling)
        {
            owner.PatrollingData?.Pause();
        }

        var targetPoint = requester.Transform.position;
        owner.GoToSomePointData?.SetPoint(targetPoint);

        if (forceRefresh || !(owner.Mover?.HasPathAndNoComplete ?? false))
        {
            owner.StopMove();
            owner.GoToPoint(targetPoint, true, -1f, false, false);
        }

        owner.GoToSomePointData?.UpdateToGo(plan.ShouldSprint);
        owner.SetPose(1f);
        owner.SetTargetMoveSpeed(plan.TargetMoveSpeed);
        owner.Sprint(plan.ShouldSprint, true);
        owner.Mover?.Sprint(plan.ShouldSprint, false);
        owner.Mover?.SetTargetMoveSpeed(plan.TargetMoveSpeed);
        return true;
    }

    private static bool TryApply(
        BotOwner owner,
        Player requester,
        FollowerCommand command,
        FollowerMovementIntent intent,
        bool driveMovement,
        bool forceRefresh)
    {
        if (owner is null || requester is null || owner.IsDead)
        {
            return false;
        }

        var botFollower = owner.BotFollower;
        if (botFollower is null)
        {
            return false;
        }

        if (botFollower.PatrolDataFollower is null)
        {
            botFollower.Activate();
        }

        botFollower.PatrolDataFollower?.InitPlayer(requester);
        botFollower.PatrolDataFollower?.SetIndex(botFollower.Index);

        if (owner.PatrollingData is not null)
        {
            var pointChooser = PatrollingData.GetPointChooser(owner, PatrolMode.simple, owner.SpawnProfileData);
            owner.PatrollingData.SetMode(PatrolMode.follower, pointChooser);
            owner.PatrollingData.Unpause();
        }

        if (!FollowerMovementActivationPolicy.TryRunBossFindAction(botFollower.BossFindAction))
        {
            FriendlyPmcCoreFollowersPlugin.Instance?.LogPluginInfo(
                $"Skipped BossFindAction during follower movement seeding for {owner.Profile?.Info?.Nickname ?? owner.ProfileId}: follower state not ready");
        }

        var playerFollowData = owner.PlayerFollowData;
        if (playerFollowData is null)
        {
            playerFollowData = new BotPlayerFollowData(owner);
            owner.PlayerFollowData = playerFollowData;
        }

        if (forceRefresh || !playerFollowData.HaveFound || !playerFollowData.IsFollower(requester))
        {
            playerFollowData.SetToFollow(requester);
        }

        var offset = FollowerOrderLayerPolicy.GetOffset(command, intent);
        playerFollowData.Offset = new Vector3(offset.X, offset.Y, offset.Z);

        if (driveMovement)
        {
            if (forceRefresh)
            {
                botFollower.PatrolDataFollower?.InitPlayer(requester);
                botFollower.PatrolDataFollower?.SetIndex(botFollower.Index);
                FollowerMovementActivationPolicy.TryRunBossFindAction(botFollower.BossFindAction);
            }

            playerFollowData.UpdateFromNode();
            botFollower.PatrolDataFollower?.ManualUpdate();
        }

        return true;
    }
}
#else
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerMovementStateApplier
{
    public static bool TrySeedMovementOrder(object owner, object requester, FollowerCommand command)
    {
        return false;
    }

    public static bool TryDriveMovementOrder(object owner, object requester, FollowerCommand command)
    {
        return false;
    }
}
#endif

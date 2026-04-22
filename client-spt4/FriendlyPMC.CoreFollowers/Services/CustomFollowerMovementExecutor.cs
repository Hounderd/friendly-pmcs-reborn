#if SPT_CLIENT
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using UnityEngine;
using UnityEngine.AI;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class CustomFollowerMovementExecutor
{
    public static bool TryExecute(
        BotOwner owner,
        Player requester,
        FollowerCommand command,
        CustomFollowerNavigationIntent navigationIntent,
        FollowerModeSettings settings)
    {
        if (owner is null || requester is null || owner.IsDead)
        {
            return false;
        }

        var distanceToPlayer = BotDebugSnapshotMapper.GetWorldPoint(owner)
            .DistanceTo(BotDebugSnapshotMapper.GetWorldPoint(requester));
        var plan = CustomFollowerMovementExecutionPolicy.Resolve(
            command,
            navigationIntent,
            distanceToPlayer,
            settings);
        var targetPoint = CustomFollowerMovementTargetPointPolicy.Resolve(
            requester,
            command,
            plan.MovementIntent);

        return TryExecutePlan(owner, requester, plan, targetPoint);
    }

    public static bool TryExecutePlan(
        BotOwner owner,
        Player requester,
        CustomFollowerMovementExecutionPlan plan,
        BotDebugWorldPoint targetPoint)
    {
        if (owner is null || requester is null || owner.IsDead)
        {
            return false;
        }

        if (!plan.ShouldMove)
        {
            owner.GoToSomePointData?.UpdateToGo(false);
            owner.SetTargetMoveSpeed(0f);
            owner.Sprint(false, true);
            owner.Mover?.Sprint(false, false);
            owner.Mover?.SetTargetMoveSpeed(0f);
            owner.StopMove();
            return true;
        }

        if (plan.ForcePathRefresh)
        {
            owner.StopMove();
        }

        var worldTarget = new Vector3(targetPoint.X, targetPoint.Y, targetPoint.Z);
        if (!TryResolveNavigablePoint(worldTarget, out var navigablePoint))
        {
            navigablePoint = requester.Transform.position;
        }

        if (UsesDirectChaseMotion(plan))
        {
            return TryExecuteDirectChase(owner, navigablePoint, plan);
        }

        MaintainMotionState(owner, plan, navigablePoint);

        owner.SetPose(1f);
        var pathStatus = owner.GoToPoint(
            navigablePoint,
            true,
            plan.ArrivalRadiusMeters,
            false,
            false);

        if (pathStatus != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        if (CustomFollowerSteeringPolicy.ShouldAlignToMovement(plan.MovementIntent))
        {
            owner.Steering?.LookToMovingDirection(60f);
        }

        return true;
    }

    public static void MaintainMotionState(
        BotOwner owner,
        CustomFollowerMovementExecutionPlan plan,
        Vector3? targetPoint = null)
    {
        if (owner is null || owner.IsDead)
        {
            return;
        }

        if (targetPoint.HasValue)
        {
            owner.GoToSomePointData?.SetPoint(targetPoint.Value);
        }

        if (UsesDirectChaseMotion(plan))
        {
            MaintainDirectChaseMotion(owner, plan);
            return;
        }

        var targetMoveSpeed = CustomFollowerMovementSpeedPolicy.Resolve(plan);
        owner.GoToSomePointData?.UpdateToGo(plan.ShouldSprint);
        owner.SetPose(1f);
        owner.SetTargetMoveSpeed(targetMoveSpeed);
        owner.Sprint(plan.ShouldSprint, true);
        owner.Mover?.Sprint(plan.ShouldSprint, false);
        owner.Mover?.SetTargetMoveSpeed(targetMoveSpeed);
        if (CustomFollowerMovementPatrolSpeedPolicy.ShouldApply(plan))
        {
            owner.PatrollingData?.SetTargetMoveSpeed();
        }
    }

    private static bool TryExecuteDirectChase(
        BotOwner owner,
        Vector3 navigablePoint,
        CustomFollowerMovementExecutionPlan plan)
    {
        owner.PatrollingData?.Pause();
        owner.GoToSomePointData?.SetPoint(navigablePoint);
        MaintainDirectChaseMotion(owner, plan);

        var pathStatus = owner.GoToPoint(
            navigablePoint,
            true,
            0.5f,
            false,
            false);

        if (pathStatus != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        owner.Steering?.LookToMovingDirection(60f);
        return true;
    }

    private static void MaintainDirectChaseMotion(
        BotOwner owner,
        CustomFollowerMovementExecutionPlan plan)
    {
        owner.SetPose(1f);
        owner.SetTargetMoveSpeed(1f);
        owner.Mover?.SetTargetMoveSpeed(1f);
        owner.Mover?.Sprint(plan.ShouldSprint, false);
    }

    private static bool UsesDirectChaseMotion(CustomFollowerMovementExecutionPlan plan)
    {
        return plan.MovementIntent is FollowerMovementIntent.CatchUpToPlayer
            or FollowerMovementIntent.ReturnToCombatRange;
    }

    private static bool TryResolveNavigablePoint(Vector3 desiredPoint, out Vector3 navigablePoint)
    {
        if (NavMesh.SamplePosition(desiredPoint, out var hit, 2.5f, NavMesh.AllAreas))
        {
            navigablePoint = hit.position;
            return true;
        }

        navigablePoint = desiredPoint;
        return false;
    }
}
#else
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class CustomFollowerMovementExecutor
{
    public static bool TryExecute(
        object owner,
        object requester,
        FollowerCommand command,
        CustomFollowerNavigationIntent navigationIntent,
        FollowerModeSettings settings)
    {
        return false;
    }
}
#endif

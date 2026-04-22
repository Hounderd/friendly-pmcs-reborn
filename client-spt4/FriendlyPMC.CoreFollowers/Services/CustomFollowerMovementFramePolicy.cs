using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public enum CustomFollowerMovementFrameAction
{
    Stop = 0,
    IssuePathCommand = 1,
    MaintainMotion = 2,
}

public readonly record struct CustomFollowerMovementFrameState(
    CustomFollowerFormationTargetState FormationTargetState,
    CustomFollowerMovementDispatchState DispatchState);

public readonly record struct CustomFollowerMovementFrameDecision(
    CustomFollowerMovementFrameAction Action,
    BotDebugWorldPoint TargetPoint,
    CustomFollowerMovementFrameState NextState);

public static class CustomFollowerMovementFramePolicy
{
    private const float IdleMovementReissueGraceSeconds = 0.35f;

    public static CustomFollowerMovementFrameDecision Evaluate(
        float now,
        CustomFollowerMovementFrameState state,
        CustomFollowerMovementExecutionPlan plan,
        CustomFollowerNavigationIntent navigationIntent,
        BotDebugWorldPoint playerPosition,
        BotDebugWorldPoint desiredTargetPoint,
        float distanceToPlayerMeters,
        bool isMoving)
    {
        if (!plan.ShouldMove)
        {
            return new CustomFollowerMovementFrameDecision(
                CustomFollowerMovementFrameAction.Stop,
                desiredTargetPoint,
                new CustomFollowerMovementFrameState(default, state.DispatchState));
        }

        var targetPoint = ResolveTargetPoint(
            state.FormationTargetState,
            playerPosition,
            desiredTargetPoint,
            plan.MovementIntent,
            out var nextFormationState);

        var dispatchResult = CustomFollowerMovementDispatchPolicy.Evaluate(
            now,
            state.DispatchState,
            navigationIntent,
            targetPoint,
            distanceToPlayerMeters);
        var shouldReissueForIdleMovement = ShouldReissueForIdleMovement(
            now,
            state.DispatchState,
            plan,
            navigationIntent,
            isMoving);
        var nextDispatchState = dispatchResult.ShouldDispatch || !shouldReissueForIdleMovement
            ? dispatchResult.NextState
            : new CustomFollowerMovementDispatchState(
                navigationIntent,
                targetPoint,
                now,
                distanceToPlayerMeters);

        var action = plan.ForcePathRefresh || dispatchResult.ShouldDispatch || shouldReissueForIdleMovement
            ? CustomFollowerMovementFrameAction.IssuePathCommand
            : CustomFollowerMovementFrameAction.MaintainMotion;

        return new CustomFollowerMovementFrameDecision(
            action,
            targetPoint,
            new CustomFollowerMovementFrameState(nextFormationState, nextDispatchState));
    }

    private static BotDebugWorldPoint ResolveTargetPoint(
        CustomFollowerFormationTargetState state,
        BotDebugWorldPoint playerPosition,
        BotDebugWorldPoint desiredTargetPoint,
        FollowerMovementIntent movementIntent,
        out CustomFollowerFormationTargetState nextState)
    {
        if (movementIntent != FollowerMovementIntent.MoveToFormation)
        {
            nextState = default;
            return desiredTargetPoint;
        }

        var formationTargetResult = CustomFollowerFormationTargetPolicy.Resolve(
            state,
            playerPosition,
            desiredTargetPoint);
        nextState = formationTargetResult.NextState;
        return formationTargetResult.TargetPoint;
    }

    private static bool ShouldReissueForIdleMovement(
        float now,
        CustomFollowerMovementDispatchState dispatchState,
        CustomFollowerMovementExecutionPlan plan,
        CustomFollowerNavigationIntent navigationIntent,
        bool isMoving)
    {
        if (!plan.ShouldMove || isMoving || navigationIntent == CustomFollowerNavigationIntent.None)
        {
            return false;
        }

        return dispatchState.LastDispatchTime > 0f
            && now - dispatchState.LastDispatchTime >= IdleMovementReissueGraceSeconds;
    }
}

using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public enum FollowerCommandExecutionMode
{
    ActivatePlayerFollowState,
    ActivateHoldPosition,
    ActivateTakeCover,
    ActivateCombatMode,
}

public readonly record struct FollowerCommandExecutionPlan(
    FollowerCommandExecutionMode Mode,
    bool ClearCurrentRequest,
    bool ClearQueuedRequests);

public static class FollowerCommandExecutionPolicy
{
    public static FollowerCommandExecutionPlan Resolve(FollowerCommand command)
    {
        return command switch
        {
            FollowerCommand.Follow => new FollowerCommandExecutionPlan(
                FollowerCommandExecutionMode.ActivatePlayerFollowState,
                ClearCurrentRequest: true,
                ClearQueuedRequests: true),
            FollowerCommand.Hold => new FollowerCommandExecutionPlan(
                FollowerCommandExecutionMode.ActivateHoldPosition,
                ClearCurrentRequest: true,
                ClearQueuedRequests: true),
            FollowerCommand.TakeCover => new FollowerCommandExecutionPlan(
                FollowerCommandExecutionMode.ActivateTakeCover,
                ClearCurrentRequest: true,
                ClearQueuedRequests: true),
            FollowerCommand.Combat => new FollowerCommandExecutionPlan(
                FollowerCommandExecutionMode.ActivateCombatMode,
                ClearCurrentRequest: true,
                ClearQueuedRequests: true),
            FollowerCommand.Regroup => new FollowerCommandExecutionPlan(
                FollowerCommandExecutionMode.ActivatePlayerFollowState,
                ClearCurrentRequest: true,
                ClearQueuedRequests: true),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
        };
    }
}

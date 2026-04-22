using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class CustomFollowerMovementPatrolSpeedPolicy
{
    public static bool ShouldApply(CustomFollowerMovementExecutionPlan plan)
    {
        if (!plan.ShouldMove)
        {
            return false;
        }

        return !plan.ShouldSprint
            && plan.MovementIntent is FollowerMovementIntent.MoveToFormation;
    }
}

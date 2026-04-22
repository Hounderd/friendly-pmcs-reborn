using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class CustomFollowerMovementSpeedPolicy
{
    private const float FullSpeed = 1f;
    private const float CruiseSpeed = 0.85f;

    public static float Resolve(CustomFollowerMovementExecutionPlan plan)
    {
        if (!plan.ShouldMove)
        {
            return 0f;
        }

        if (plan.ShouldSprint
            || plan.MovementIntent is FollowerMovementIntent.CatchUpToPlayer
                or FollowerMovementIntent.ReturnToCombatRange)
        {
            return FullSpeed;
        }

        return CruiseSpeed;
    }
}

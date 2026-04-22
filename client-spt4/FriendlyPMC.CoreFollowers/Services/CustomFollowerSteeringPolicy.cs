using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class CustomFollowerSteeringPolicy
{
    public static bool ShouldAlignToMovement(FollowerMovementIntent movementIntent)
    {
        return movementIntent is not FollowerMovementIntent.MoveToFormation;
    }
}

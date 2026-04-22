using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public enum FollowerMovementIntent
{
    HoldFormation = 0,
    MoveToFormation = 1,
    CatchUpToPlayer = 2,
    ReturnToCombatRange = 3,
}

public static class FollowerCatchUpPolicy
{
    public static FollowerMovementIntent ResolveMovementIntent(
        FollowerCommand command,
        float distanceToPlayerMeters,
        FollowerModeSettings settings)
    {
        return command switch
        {
            FollowerCommand.Follow when distanceToPlayerMeters <= settings.FollowDeadzoneMeters
                => FollowerMovementIntent.HoldFormation,
            FollowerCommand.Follow when distanceToPlayerMeters >= settings.EffectiveCatchUpDistanceMeters
                => FollowerMovementIntent.CatchUpToPlayer,
            FollowerCommand.Follow
                => FollowerMovementIntent.MoveToFormation,
            FollowerCommand.Combat when distanceToPlayerMeters > settings.CombatMaxRangeMeters
                => FollowerMovementIntent.ReturnToCombatRange,
            _ => FollowerMovementIntent.HoldFormation,
        };
    }
}

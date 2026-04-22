using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public enum FollowerModeDisposition
{
    ForceFollowMovement,
    ForceCatchUpMovement,
    ForceHoldAnchor,
    ForceReturnToCombatRange,
    AllowCombatPursuit,
}

public static class FollowerModePolicy
{
    public static FollowerModeDisposition ResolveDisposition(
        FollowerCommand command,
        bool haveActionableEnemy,
        float distanceToPlayerMeters,
        float distanceToHoldAnchorMeters,
        FollowerModeSettings settings)
    {
        return command switch
        {   
            FollowerCommand.Follow => ResolveFollowDisposition(
                haveActionableEnemy,
                distanceToPlayerMeters,
                settings),
            FollowerCommand.Hold => !haveActionableEnemy || distanceToHoldAnchorMeters > settings.HoldRadiusMeters
                ? FollowerModeDisposition.ForceHoldAnchor
                : FollowerModeDisposition.AllowCombatPursuit,
            FollowerCommand.TakeCover => !haveActionableEnemy || distanceToHoldAnchorMeters > settings.HoldRadiusMeters
                ? FollowerModeDisposition.ForceHoldAnchor
                : FollowerModeDisposition.AllowCombatPursuit,
            FollowerCommand.Combat => distanceToPlayerMeters > settings.CombatMaxRangeMeters
                ? FollowerModeDisposition.ForceReturnToCombatRange
                : FollowerModeDisposition.AllowCombatPursuit,
            FollowerCommand.Regroup => FollowerModeDisposition.ForceFollowMovement,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
        };
    }

    private static FollowerModeDisposition ResolveFollowDisposition(
        bool haveActionableEnemy,
        float distanceToPlayerMeters,
        FollowerModeSettings settings)
    {
        if (haveActionableEnemy && distanceToPlayerMeters <= settings.CombatMaxRangeMeters)
        {
            return FollowerModeDisposition.AllowCombatPursuit;
        }

        return distanceToPlayerMeters >= settings.EffectiveCatchUpDistanceMeters
            ? FollowerModeDisposition.ForceCatchUpMovement
            : FollowerModeDisposition.ForceFollowMovement;
    }
}

using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerRuntimeModeEnforcementPolicy
{
    public static bool ShouldDriveMovement(FollowerCommand command, FollowerModeDisposition disposition)
    {
        return disposition is FollowerModeDisposition.ForceFollowMovement
            or FollowerModeDisposition.ForceCatchUpMovement
            or FollowerModeDisposition.ForceReturnToCombatRange;
    }

    public static bool ShouldRefreshHold(FollowerCommand command, FollowerModeDisposition disposition)
    {
        return command is FollowerCommand.Hold or FollowerCommand.TakeCover
            && disposition == FollowerModeDisposition.ForceHoldAnchor;
    }

    public static bool ShouldAllowCombatAutonomy(FollowerCommand command, FollowerModeDisposition disposition)
    {
        return disposition == FollowerModeDisposition.AllowCombatPursuit;
    }
}

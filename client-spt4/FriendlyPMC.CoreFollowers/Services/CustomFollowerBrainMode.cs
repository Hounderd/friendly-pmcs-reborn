namespace FriendlyPMC.CoreFollowers.Services;

public enum CustomFollowerBrainMode
{
    FollowFormation = 0,
    FollowCatchUp = 1,
    HoldDefendLocal = 2,
    HoldReturnToAnchor = 3,
    CombatPursue = 4,
    CombatReturnToRange = 5,
    RecoverNavigation = 6,
}

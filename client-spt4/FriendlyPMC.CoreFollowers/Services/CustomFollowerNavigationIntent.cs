namespace FriendlyPMC.CoreFollowers.Services;

public enum CustomFollowerNavigationIntent
{
    None = 0,
    MoveToFormation = 1,
    CatchUpToPlayer = 2,
    ReturnToAnchor = 3,
    ReturnToCombatRange = 4,
    RepathAndRecover = 5,
}

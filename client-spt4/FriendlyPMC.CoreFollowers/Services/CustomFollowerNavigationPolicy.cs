namespace FriendlyPMC.CoreFollowers.Services;

public static class CustomFollowerNavigationPolicy
{
    public static CustomFollowerNavigationIntent Resolve(CustomFollowerBrainDecision decision)
    {
        return decision.Mode switch
        {
            CustomFollowerBrainMode.FollowFormation => CustomFollowerNavigationIntent.MoveToFormation,
            CustomFollowerBrainMode.FollowCatchUp => CustomFollowerNavigationIntent.CatchUpToPlayer,
            CustomFollowerBrainMode.HoldReturnToAnchor => CustomFollowerNavigationIntent.ReturnToAnchor,
            CustomFollowerBrainMode.CombatReturnToRange => CustomFollowerNavigationIntent.ReturnToCombatRange,
            CustomFollowerBrainMode.RecoverNavigation => CustomFollowerNavigationIntent.RepathAndRecover,
            _ => CustomFollowerNavigationIntent.None,
        };
    }
}

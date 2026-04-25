namespace FriendlyPMC.CoreFollowers.Services;

public static class CustomFollowerPatrolSuppressionPolicy
{
    public static bool ShouldSuppress(CustomFollowerBrainMode mode)
    {
        return mode is
            CustomFollowerBrainMode.FollowFormation or
            CustomFollowerBrainMode.FollowCatchUp or
            CustomFollowerBrainMode.HoldDefendLocal or
            CustomFollowerBrainMode.HoldReturnToAnchor or
            CustomFollowerBrainMode.RecoverNavigation;
    }
}

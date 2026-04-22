namespace FriendlyPMC.CoreFollowers.Services;

internal static class CustomFollowerCatchUpAuthorityPolicy
{
    private const float StockFollowAuthorityDistanceMeters = 30f;

    public static bool ShouldUseStockFollowAuthority(
        CustomFollowerNavigationIntent navigationIntent,
        float distanceToPlayerMeters)
    {
        return navigationIntent == CustomFollowerNavigationIntent.RepathAndRecover
            || (navigationIntent == CustomFollowerNavigationIntent.CatchUpToPlayer
                && distanceToPlayerMeters >= StockFollowAuthorityDistanceMeters);
    }
}

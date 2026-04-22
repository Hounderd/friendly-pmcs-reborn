namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerProfileScreenRefreshCoordinator
{
    private readonly Func<string, bool> isViewingProfile;
    private readonly Func<string, Task<object?>> requestFreshControllerAsync;
    private readonly Func<string, object?> getVisibleScreen;
    private readonly Func<object, object, Task> applyVisibleScreenRefreshAsync;
    private readonly Action refreshFriends;
    private readonly Action<string>? logInfo;

    public FollowerProfileScreenRefreshCoordinator(
        Func<string, bool> isViewingProfile,
        Func<string, Task<object?>> requestFreshControllerAsync,
        Func<string, object?> getVisibleScreen,
        Func<object, object, Task> applyVisibleScreenRefreshAsync,
        Action refreshFriends,
        Action<string>? logInfo)
    {
        this.isViewingProfile = isViewingProfile;
        this.requestFreshControllerAsync = requestFreshControllerAsync;
        this.getVisibleScreen = getVisibleScreen;
        this.applyVisibleScreenRefreshAsync = applyVisibleScreenRefreshAsync;
        this.refreshFriends = refreshFriends;
        this.logInfo = logInfo;
    }

    public async Task RefreshAfterInventoryMoveAsync(string followerAid)
    {
        if (string.IsNullOrWhiteSpace(followerAid) || !isViewingProfile(followerAid))
        {
            return;
        }

        logInfo?.Invoke(
            $"Refreshing visible follower profile after inventory move: aid={followerAid}");
        refreshFriends();

        var refreshedController = await requestFreshControllerAsync(followerAid);
        if (refreshedController is null)
        {
            return;
        }

        var visibleScreen = getVisibleScreen(followerAid);
        if (visibleScreen is null)
        {
            logInfo?.Invoke(
                $"Visible follower profile screen was not available for refresh: aid={followerAid}");
            return;
        }

        await applyVisibleScreenRefreshAsync(visibleScreen, refreshedController);
        logInfo?.Invoke(
            $"Applied visible follower profile refresh: aid={followerAid}");
    }
}

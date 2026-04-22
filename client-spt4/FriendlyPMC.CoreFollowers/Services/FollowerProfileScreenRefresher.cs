#if SPT_CLIENT
using System.Reflection;
using EFT.InventoryLogic;
using FriendlyPMC.CoreFollowers.Patches;
using HarmonyLib;

namespace FriendlyPMC.CoreFollowers.Services;

public interface IFollowerProfileScreenRefresher
{
    Task RefreshAfterInventoryMoveAsync(string followerAid);
}

internal sealed class FollowerProfileScreenRefresher : IFollowerProfileScreenRefresher
{
    private const EItemViewType ProfileViewType = (EItemViewType)25;
    private static readonly Type? ItemUiContextType = AccessTools.TypeByName("EFT.UI.ItemUiContext");
    private static readonly PropertyInfo? ItemUiContextInstanceProperty =
        ItemUiContextType is null ? null : AccessTools.Property(ItemUiContextType, "Instance");
    private static readonly MethodInfo? ShowPlayerProfileScreenMethod =
        ItemUiContextType is null ? null : AccessTools.Method(ItemUiContextType, "ShowPlayerProfileScreen", new[] { typeof(string), typeof(EItemViewType) });
    private static readonly Type? OtherPlayerProfileScreenType =
        AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen");
    private static readonly Type? OtherPlayerProfileScreenControllerType =
        AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen+GClass3883");
    private static readonly MethodInfo? OtherPlayerProfileScreenShowControllerMethod =
        OtherPlayerProfileScreenType is null || OtherPlayerProfileScreenControllerType is null
            ? null
            : AccessTools.Method(OtherPlayerProfileScreenType, "Show", new[] { OtherPlayerProfileScreenControllerType });

    private readonly Func<string, Task<object?>> requestFreshControllerAsync;
    private readonly Func<string, object?> getVisibleScreen;
    private readonly Func<object, object, Task> applyVisibleScreenRefreshAsync;
    private readonly Action refreshFriends;
    private readonly Action<string>? logInfo;
    private readonly FollowerProfileScreenRefreshCoordinator coordinator;

    public FollowerProfileScreenRefresher(
        Func<string, Task<object?>>? requestFreshControllerAsync = null,
        Func<string, object?>? getVisibleScreen = null,
        Func<object, object, Task>? applyVisibleScreenRefreshAsync = null,
        Action? refreshFriends = null,
        Action<string>? logInfo = null)
    {
        this.requestFreshControllerAsync = requestFreshControllerAsync ?? RequestFreshControllerAsync;
        this.getVisibleScreen = getVisibleScreen ?? FollowerProfileScreenTracker.GetVisibleScreen;
        this.applyVisibleScreenRefreshAsync = applyVisibleScreenRefreshAsync ?? ApplyVisibleScreenRefreshAsync;
        this.refreshFriends = refreshFriends ?? FollowerSocialFriendRefresh.RefreshSoon;
        this.logInfo = logInfo ?? FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo;
        coordinator = new FollowerProfileScreenRefreshCoordinator(
            FollowerProfileScreenTracker.IsViewingProfile,
            this.requestFreshControllerAsync,
            this.getVisibleScreen,
            this.applyVisibleScreenRefreshAsync,
            this.refreshFriends,
            this.logInfo);
    }

    public async Task RefreshAfterInventoryMoveAsync(string followerAid)
    {
        await coordinator.RefreshAfterInventoryMoveAsync(followerAid);
    }

    private static async Task<object?> RequestFreshControllerAsync(string followerAid)
    {
        var itemUiContext = ItemUiContextInstanceProperty?.GetValue(null);
        if (itemUiContext is null || ShowPlayerProfileScreenMethod is null)
        {
            return null;
        }

        if (ShowPlayerProfileScreenMethod.Invoke(itemUiContext, new object[] { followerAid, ProfileViewType }) is Task refreshTask)
        {
            await refreshTask;
            return refreshTask.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)?.GetValue(refreshTask);
        }

        return null;
    }

    private static Task ApplyVisibleScreenRefreshAsync(object screen, object controller)
    {
        if (OtherPlayerProfileScreenShowControllerMethod is null)
        {
            return Task.CompletedTask;
        }

        if (OtherPlayerProfileScreenShowControllerMethod.Invoke(screen, new[] { controller }) is Task refreshTask)
        {
            return refreshTask;
        }

        return Task.CompletedTask;
    }
}

internal static class FollowerProfileScreenTracker
{
    private static string currentAccountId = string.Empty;
    private static WeakReference<object>? currentScreen;

    public static void SetVisibleProfile(string? accountId, object? screen = null)
    {
        currentAccountId = FollowerVisibleProfileIdPolicy.Normalize(
            accountId,
            FollowerSocialFriendRefresh.GetFriendReferences());
        if (screen is not null)
        {
            currentScreen = new WeakReference<object>(screen);
        }
    }

    public static void ClearVisibleProfile(string? accountId)
    {
        var normalizedAccountId = FollowerVisibleProfileIdPolicy.Normalize(
            accountId,
            FollowerSocialFriendRefresh.GetFriendReferences());
        if (string.IsNullOrWhiteSpace(normalizedAccountId)
            || string.Equals(currentAccountId, normalizedAccountId, StringComparison.Ordinal))
        {
            currentAccountId = string.Empty;
            currentScreen = null;
        }
    }

    public static bool IsViewingProfile(string? accountId)
    {
        var normalizedAccountId = FollowerVisibleProfileIdPolicy.Normalize(
            accountId,
            FollowerSocialFriendRefresh.GetFriendReferences());
        return !string.IsNullOrWhiteSpace(normalizedAccountId)
            && string.Equals(currentAccountId, normalizedAccountId, StringComparison.Ordinal);
    }

    public static object? GetVisibleScreen(string? accountId)
    {
        if (!IsViewingProfile(accountId))
        {
            return null;
        }

        if (currentScreen?.TryGetTarget(out var screen) == true)
        {
            return screen;
        }

        return null;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

public interface IFollowerProfileScreenRefresher
{
    Task RefreshAfterInventoryMoveAsync(string followerAid);
}

internal sealed class FollowerProfileScreenRefresher : IFollowerProfileScreenRefresher
{
    public Task RefreshAfterInventoryMoveAsync(string followerAid)
    {
        return Task.CompletedTask;
    }
}
#endif

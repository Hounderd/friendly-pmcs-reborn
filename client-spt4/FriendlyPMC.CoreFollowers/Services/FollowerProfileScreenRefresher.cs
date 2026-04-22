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

    public async Task RefreshAfterInventoryMoveAsync(string followerAid)
    {
        if (string.IsNullOrWhiteSpace(followerAid) || !FollowerProfileScreenTracker.IsViewingProfile(followerAid))
        {
            return;
        }

        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Refreshing visible follower profile after inventory move: aid={followerAid}");
        FollowerSocialFriendRefresh.RefreshSoon();

        var itemUiContext = ItemUiContextInstanceProperty?.GetValue(null);
        if (itemUiContext is null || ShowPlayerProfileScreenMethod is null)
        {
            return;
        }

        if (ShowPlayerProfileScreenMethod.Invoke(itemUiContext, new object[] { followerAid, ProfileViewType }) is Task refreshTask)
        {
            await refreshTask;
        }
    }
}

internal static class FollowerProfileScreenTracker
{
    private static string currentAccountId = string.Empty;

    public static void SetVisibleProfile(string? accountId)
    {
        currentAccountId = accountId?.Trim() ?? string.Empty;
    }

    public static void ClearVisibleProfile(string? accountId)
    {
        var normalizedAccountId = accountId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedAccountId)
            || string.Equals(currentAccountId, normalizedAccountId, StringComparison.Ordinal))
        {
            currentAccountId = string.Empty;
        }
    }

    public static bool IsViewingProfile(string? accountId)
    {
        var normalizedAccountId = accountId?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(normalizedAccountId)
            && string.Equals(currentAccountId, normalizedAccountId, StringComparison.Ordinal);
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

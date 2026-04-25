#if SPT_CLIENT
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerMainMenuRosterInjector
{
    public static void TryInject(object screen, Profile profile, IReadOnlyList<FollowerSnapshotDto> followers, Action<string>? logInfo, Action<string, Exception>? logError)
    {
        try
        {
            if (followers.Count == 0)
            {
                return;
            }

            var anchorText = TryResolveProfileNameText(screen, profile)
                ?? TryResolveButtonText(screen, "_playerButton");
            var container = anchorText?.transform.parent;
            if (container is null || anchorText is null)
            {
                return;
            }

            FollowerRosterUiInjector.TryInject(container, anchorText, followers, logInfo, "main-menu");
        }
        catch (Exception ex)
        {
            logError?.Invoke("Failed to inject follower main menu roster", ex);
        }
    }

    private static TextMeshProUGUI? TryResolveProfileNameText(object screen, Profile profile)
    {
        var nickname = profile.Info?.Nickname;
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return null;
        }

        return (screen as Component)?
            .GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true)
            .FirstOrDefault(text => text.text.IndexOf(nickname, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static TextMeshProUGUI? TryResolveButtonText(object screen, string fieldName)
    {
        var button = AccessTools.Field(screen.GetType(), fieldName)?.GetValue(screen) as Component;
        return button?.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
    }
}
#else
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerMainMenuRosterInjector
{
    public static void TryInject(object screen, object profile, IReadOnlyList<FollowerSnapshotDto> followers, Action<string>? logInfo, Action<string, Exception>? logError)
    {
    }
}
#endif

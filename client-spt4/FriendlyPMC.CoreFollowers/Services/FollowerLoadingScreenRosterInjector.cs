#if SPT_CLIENT
using FriendlyPMC.CoreFollowers.Models;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerLoadingScreenRosterInjector
{
    private const string RosterObjectName = "FriendlyFollowerLoadingRoster";

    public static void TryInject(object screen, IReadOnlyList<FollowerSnapshotDto> followers, Action<string>? logInfo, Action<string, Exception>? logError)
    {
        try
        {
            var lines = FollowerLoadingScreenRosterPolicy.BuildLines(followers);
            var container = TryResolvePlayersContainer(screen);
            if (container is null)
            {
                return;
            }

            DestroyExisting(container);
            if (lines.Count == 0)
            {
                return;
            }

            var templateText = container.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text.text));
            if (templateText is null)
            {
                return;
            }

            FollowerRosterUiInjector.TryInject(container, templateText, followers, logInfo, "loading");
        }
        catch (Exception ex)
        {
            logError?.Invoke("Failed to inject follower loading roster", ex);
        }
    }

    private static Transform? TryResolvePlayersContainer(object screen)
    {
        var partyInfoPanel = AccessTools.Field(screen.GetType(), "_partyInfoPanel")?.GetValue(screen) as Component;
        if (partyInfoPanel is null)
        {
            return null;
        }

        return AccessTools.Field(partyInfoPanel.GetType(), "_playersContainer")?.GetValue(partyInfoPanel) as Transform;
    }

    private static void DestroyExisting(Transform container)
    {
        var existing = container.Find(RosterObjectName);
        if (existing is not null)
        {
            UnityEngine.Object.Destroy(existing.gameObject);
        }
    }

}
#else
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerLoadingScreenRosterInjector
{
    public static void TryInject(object screen, IReadOnlyList<FollowerSnapshotDto> followers, Action<string>? logInfo, Action<string, Exception>? logError)
    {
    }
}
#endif

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
            DestroyExistingUnderScreen(screen);
            if (followers.Count == 0)
            {
                return;
            }

            var versionText = TryResolveVersionText(screen);
            var profileText = TryResolveProfileNameText(screen, profile);
            var buttonText = TryResolveButtonText(screen, "_playerButton");
            var anchorText = versionText ?? profileText ?? buttonText ?? TryResolveAnyMenuText(screen);
            if (anchorText is null)
            {
                logInfo?.Invoke("Follower main menu roster skipped: no text template found");
                return;
            }

            var container = versionText?.transform.parent ?? (screen as Component)?.transform ?? anchorText.transform.parent;
            if (container is null)
            {
                logInfo?.Invoke("Follower main menu roster skipped: no UI container found");
                return;
            }

            var fontSize = FollowerMainMenuRosterLayoutPolicy.ResolveFollowerFontSize(anchorText.fontSize);
            var injected = FollowerRosterUiInjector.TryInject(container, anchorText, followers, logInfo, "main-menu", fontSize);
            if (injected > 0)
            {
                PositionRootLevelRoster(container, versionText);
            }
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

    private static TextMeshProUGUI? TryResolveVersionText(object screen)
    {
        return (screen as Component)?
            .GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true)
            .FirstOrDefault(text =>
                !string.IsNullOrWhiteSpace(text.text)
                && text.text.IndexOf("SPT", StringComparison.OrdinalIgnoreCase) >= 0
                && text.text.IndexOf("SAIN", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static TextMeshProUGUI? TryResolveButtonText(object screen, string fieldName)
    {
        var button = AccessTools.Field(screen.GetType(), fieldName)?.GetValue(screen) as Component;
        return button?.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
    }

    private static TextMeshProUGUI? TryResolveAnyMenuText(object screen)
    {
        return (screen as Component)?
            .GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text.text));
    }

    private static void PositionRootLevelRoster(Transform container, TextMeshProUGUI? versionText)
    {
        var roster = container.Find("FriendlyFollowerRoster");
        if (roster is null)
        {
            return;
        }

        var rect = roster.GetComponent<RectTransform>();
        if (rect is null)
        {
            return;
        }

        var layout = FollowerMainMenuRosterLayoutPolicy.Resolve();
        rect.anchorMin = new Vector2(layout.AnchorMinX, layout.AnchorMinY);
        rect.anchorMax = new Vector2(layout.AnchorMaxX, layout.AnchorMaxY);
        rect.pivot = new Vector2(layout.PivotX, layout.PivotY);
        if (versionText?.rectTransform is { } versionRect)
        {
            var position = FollowerMainMenuRosterLayoutPolicy.ResolvePositionFromVersionText(
                versionRect.anchoredPosition.x,
                versionRect.anchoredPosition.y);
            rect.anchoredPosition = new Vector2(position.X, position.Y);
            return;
        }

        rect.anchoredPosition = new Vector2(layout.PositionX, layout.PositionY);
    }

    private static void DestroyExistingUnderScreen(object screen)
    {
        var root = (screen as Component)?.transform;
        if (root is null)
        {
            return;
        }

        foreach (var existing in root.GetComponentsInChildren<Transform>(includeInactive: true)
                     .Where(child => child.name == "FriendlyFollowerRoster")
                     .ToArray())
        {
            UnityEngine.Object.Destroy(existing.gameObject);
        }
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

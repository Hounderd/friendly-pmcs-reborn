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

            var root = new GameObject(RosterObjectName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            root.transform.SetParent(container, worldPositionStays: false);
            root.transform.SetAsLastSibling();

            var background = root.GetComponent<Image>();
            background.color = new Color32(5, 9, 8, 165);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = 2f;
            layout.padding = new RectOffset(8, 10, 5, 5);

            var fitter = root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var line in lines)
            {
                CreateLine(root.transform, templateText, line);
            }

            logInfo?.Invoke($"Injected follower loading roster: count={lines.Count}");
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

    private static void CreateLine(Transform parent, TextMeshProUGUI templateText, string text)
    {
        var lineObject = new GameObject("FriendlyFollowerLoadingRosterLine", typeof(RectTransform), typeof(TextMeshProUGUI));
        lineObject.transform.SetParent(parent, worldPositionStays: false);

        var lineText = lineObject.GetComponent<TextMeshProUGUI>();
        lineText.font = templateText.font;
        lineText.fontSize = templateText.fontSize;
        lineText.fontStyle = templateText.fontStyle;
        lineText.color = new Color32(210, 210, 200, 255);
        lineText.alignment = templateText.alignment;
        lineText.richText = true;
        lineText.text = text;
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

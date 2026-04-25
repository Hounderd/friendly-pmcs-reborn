#if SPT_CLIENT
using FriendlyPMC.CoreFollowers.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerRosterUiInjector
{
    private const string RosterObjectName = "FriendlyFollowerRoster";

    public static int TryInject(
        Transform container,
        TextMeshProUGUI templateText,
        IEnumerable<FollowerSnapshotDto> followers,
        Action<string>? logInfo,
        string context,
        float? fontSizeOverride = null)
    {
        var lines = FollowerLoadingScreenRosterPolicy.BuildLines(followers);
        DestroyExisting(container);
        if (lines.Count == 0)
        {
            return 0;
        }

        var root = new GameObject(RosterObjectName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(container, worldPositionStays: false);
        root.transform.SetAsLastSibling();

        var rectTransform = root.GetComponent<RectTransform>();
        rectTransform.localScale = Vector3.one;

        var background = root.GetComponent<Image>();
        background.color = new Color32(4, 8, 7, 118);

        var layout = root.GetComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 1f;
        layout.padding = new RectOffset(9, 11, 5, 6);

        var fitter = root.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateHeader(root.transform, templateText, fontSizeOverride);
        foreach (var line in lines)
        {
            CreateLine(root.transform, templateText, line, fontSizeOverride);
        }

        logInfo?.Invoke($"Injected follower roster UI: context={context}, count={lines.Count}");
        return lines.Count;
    }

    private static void DestroyExisting(Transform container)
    {
        var existing = container.Find(RosterObjectName);
        if (existing is not null)
        {
            UnityEngine.Object.Destroy(existing.gameObject);
        }
    }

    private static void CreateHeader(Transform parent, TextMeshProUGUI templateText, float? fontSizeOverride)
    {
        var headerObject = new GameObject("FriendlyFollowerRosterHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObject.transform.SetParent(parent, worldPositionStays: false);

        var headerText = headerObject.GetComponent<TextMeshProUGUI>();
        CopyTextStyle(templateText, headerText);
        headerText.fontSize = fontSizeOverride.HasValue
            ? MathF.Max(10f, fontSizeOverride.Value * 0.72f)
            : MathF.Max(10f, templateText.fontSize * 0.72f);
        headerText.color = new Color32(111, 124, 113, 230);
        headerText.text = "FOLLOWERS";
    }

    private static void CreateLine(Transform parent, TextMeshProUGUI templateText, string text, float? fontSizeOverride)
    {
        var lineObject = new GameObject("FriendlyFollowerRosterLine", typeof(RectTransform), typeof(TextMeshProUGUI));
        lineObject.transform.SetParent(parent, worldPositionStays: false);

        var lineText = lineObject.GetComponent<TextMeshProUGUI>();
        CopyTextStyle(templateText, lineText);
        if (fontSizeOverride.HasValue)
        {
            lineText.fontSize = fontSizeOverride.Value;
        }

        lineText.color = new Color32(210, 210, 200, 255);
        lineText.richText = true;
        lineText.text = text;
    }

    private static void CopyTextStyle(TextMeshProUGUI templateText, TextMeshProUGUI targetText)
    {
        targetText.font = templateText.font;
        targetText.fontSize = templateText.fontSize;
        targetText.fontStyle = templateText.fontStyle;
        targetText.alignment = templateText.alignment;
    }
}
#else
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerRosterUiInjector
{
    public static int TryInject(
        object container,
        object templateText,
        IEnumerable<FollowerSnapshotDto> followers,
        Action<string>? logInfo,
        string context,
        float? fontSizeOverride = null)
    {
        return 0;
    }
}
#endif

#if SPT_CLIENT
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerPlateView
{
    private const float HealthBarWidth = 144f;
    private const float HealthBarHeight = 10f;
    private const float HealthBarInset = 1f;

    private readonly RectTransform root;
    private readonly Image background;
    private readonly TextMeshProUGUI nameText;
    private readonly Image factionBadge;
    private readonly TextMeshProUGUI factionText;
    private readonly Image healthBarBackground;
    private readonly Image healthBarFill;
    private readonly RectTransform healthBarFillRect;
    private readonly TextMeshProUGUI healthText;

    private FollowerPlateView(
        RectTransform root,
        Image background,
        TextMeshProUGUI nameText,
        Image factionBadge,
        TextMeshProUGUI factionText,
        Image healthBarBackground,
        Image healthBarFill,
        RectTransform healthBarFillRect,
        TextMeshProUGUI healthText)
    {
        this.root = root;
        this.background = background;
        this.nameText = nameText;
        this.factionBadge = factionBadge;
        this.factionText = factionText;
        this.healthBarBackground = healthBarBackground;
        this.healthBarFill = healthBarFill;
        this.healthBarFillRect = healthBarFillRect;
        this.healthText = healthText;
    }

    public static FollowerPlateView Create(RectTransform parent, TMP_FontAsset? fontAsset)
    {
        var rootObject = new GameObject("FriendlyFollowerPlate", typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);
        var root = rootObject.GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(196f, 60f);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);

        var background = AddImage("Background", root, new Color(0f, 0f, 0f, 0.42f));
        Stretch(background.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f));

        var factionBadge = AddImage("FactionBadge", root, new Color(0.6f, 0.6f, 0.6f, 0.95f));
        factionBadge.rectTransform.anchorMin = new Vector2(0f, 1f);
        factionBadge.rectTransform.anchorMax = new Vector2(0f, 1f);
        factionBadge.rectTransform.pivot = new Vector2(0f, 1f);
        factionBadge.rectTransform.anchoredPosition = new Vector2(8f, -7f);
        factionBadge.rectTransform.sizeDelta = new Vector2(48f, 18f);

        var factionText = AddText("FactionText", factionBadge.rectTransform, fontAsset, 10f, FontStyles.Bold);
        factionText.alignment = TextAlignmentOptions.Center;
        Stretch(factionText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f));

        var nameText = AddText("NameText", root, fontAsset, 16f, FontStyles.Bold);
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameText.rectTransform.anchorMax = new Vector2(0f, 1f);
        nameText.rectTransform.pivot = new Vector2(0f, 1f);
        nameText.rectTransform.anchoredPosition = new Vector2(62f, -5f);
        nameText.rectTransform.sizeDelta = new Vector2(124f, 22f);

        var healthBarBackground = AddImage("HealthBarBackground", root, new Color(0f, 0f, 0f, 0.55f));
        healthBarBackground.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        healthBarBackground.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        healthBarBackground.rectTransform.pivot = new Vector2(0.5f, 0f);
        healthBarBackground.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        healthBarBackground.rectTransform.sizeDelta = new Vector2(HealthBarWidth, HealthBarHeight);

        var healthBarFill = AddImage("HealthBarFill", healthBarBackground.rectTransform, new Color(0.2f, 0.95f, 0.35f, 1f));
        var healthBarFillRect = healthBarFill.rectTransform;
        healthBarFillRect.anchorMin = new Vector2(0f, 0.5f);
        healthBarFillRect.anchorMax = new Vector2(0f, 0.5f);
        healthBarFillRect.pivot = new Vector2(0f, 0.5f);
        healthBarFillRect.anchoredPosition = new Vector2(HealthBarInset, 0f);
        healthBarFillRect.sizeDelta = new Vector2(HealthBarWidth - (HealthBarInset * 2f), HealthBarHeight - (HealthBarInset * 2f));

        var healthText = AddText("HealthText", root, fontAsset, 11f, FontStyles.Bold);
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        healthText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        healthText.rectTransform.pivot = new Vector2(0.5f, 0f);
        healthText.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        healthText.rectTransform.sizeDelta = new Vector2(144f, 14f);

        return new FollowerPlateView(
            root,
            background,
            nameText,
            factionBadge,
            factionText,
            healthBarBackground,
            healthBarFill,
            healthBarFillRect,
            healthText);
    }

    public void SetActive(bool active)
    {
        if (root.gameObject.activeSelf != active)
        {
            root.gameObject.SetActive(active);
        }
    }

    public void UpdateContent(
        string nickname,
        string side,
        int healthPercent,
        FollowerPlateSettings settings)
    {
        nameText.SetText(nickname);

        var factionLabel = string.Equals(side, "Usec", StringComparison.OrdinalIgnoreCase)
            ? "USEC"
            : string.Equals(side, "Bear", StringComparison.OrdinalIgnoreCase)
                ? "BEAR"
                : side.ToUpperInvariant();

        factionText.SetText(factionLabel);
        factionBadge.color = FollowerPlateProjection.GetFactionColor(side);
        factionBadge.gameObject.SetActive(settings.ShowFactionBadge);

        var healthColor = FollowerPlateProjection.GetHealthColor(healthPercent);
        var healthFillWidth = FollowerPlateHealthBarPolicy.ResolveFillWidth(
            HealthBarWidth - (HealthBarInset * 2f),
            healthPercent);
        healthBarFill.color = healthColor;
        healthBarFillRect.sizeDelta = new Vector2(healthFillWidth, HealthBarHeight - (HealthBarInset * 2f));
        healthBarBackground.gameObject.SetActive(settings.ShowHealthBar);

        healthText.SetText("{0}%", healthPercent);
        healthText.color = healthColor;
        healthText.gameObject.SetActive(settings.ShowHealthNumber);
    }

    public void UpdatePosition(Vector2 canvasPosition, float scale)
    {
        root.anchoredPosition = canvasPosition;
        root.localScale = new Vector3(scale, scale, 1f);
    }

    public void Destroy()
    {
        if (root is not null)
        {
            UnityEngine.Object.Destroy(root.gameObject);
        }
    }

    private static Image AddImage(string name, RectTransform parent, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI AddText(string name, RectTransform parent, TMP_FontAsset? fontAsset, float fontSize, FontStyles fontStyle)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = Color.white;
        return text;
    }

    private static void Stretch(RectTransform rectTransform, Vector2 minOffset, Vector2 maxOffset)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = minOffset;
        rectTransform.offsetMax = -maxOffset;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerPlateView
{
}
#endif

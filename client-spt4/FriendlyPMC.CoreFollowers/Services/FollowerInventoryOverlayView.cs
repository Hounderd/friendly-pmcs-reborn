#if SPT_CLIENT
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerInventoryOverlayView : IFollowerInventoryRuntimeView
{
    private readonly GameObject rootObject;
    private readonly FollowerInventoryScreenActions actions;
    private readonly TextMeshProUGUI titleText;
    private readonly TextMeshProUGUI statusText;
    private readonly TextMeshProUGUI errorText;
    private readonly TextMeshProUGUI debugText;
    private readonly Button primaryActionButton;
    private readonly TextMeshProUGUI primaryActionLabel;
    private readonly GameObject targetClusterObject;
    private readonly RectTransform targetButtonsRoot;
    private readonly TextMeshProUGUI playerSummaryText;
    private readonly RectTransform playerItemsRoot;
    private readonly TextMeshProUGUI followerSummaryText;
    private readonly RectTransform followerItemsRoot;
    private readonly TMP_FontAsset? fontAsset;

    private FollowerInventoryOverlayView(
        GameObject rootObject,
        FollowerInventoryScreenActions actions,
        TextMeshProUGUI titleText,
        TextMeshProUGUI statusText,
        TextMeshProUGUI errorText,
        TextMeshProUGUI debugText,
        Button primaryActionButton,
        TextMeshProUGUI primaryActionLabel,
        GameObject targetClusterObject,
        RectTransform targetButtonsRoot,
        TextMeshProUGUI playerSummaryText,
        RectTransform playerItemsRoot,
        TextMeshProUGUI followerSummaryText,
        RectTransform followerItemsRoot,
        TMP_FontAsset? fontAsset)
    {
        this.rootObject = rootObject;
        this.actions = actions;
        this.titleText = titleText;
        this.statusText = statusText;
        this.errorText = errorText;
        this.debugText = debugText;
        this.primaryActionButton = primaryActionButton;
        this.primaryActionLabel = primaryActionLabel;
        this.targetClusterObject = targetClusterObject;
        this.targetButtonsRoot = targetButtonsRoot;
        this.playerSummaryText = playerSummaryText;
        this.playerItemsRoot = playerItemsRoot;
        this.followerSummaryText = followerSummaryText;
        this.followerItemsRoot = followerItemsRoot;
        this.fontAsset = fontAsset;
    }

    public static FollowerInventoryOverlayView Create(object? hostScreen, TMP_FontAsset? fontAsset, FollowerInventoryScreenActions actions)
    {
        var parent = ResolveParent(hostScreen);
        var overlayObject = new GameObject("FriendlyFollowerInventoryOverlay", typeof(RectTransform));
        overlayObject.transform.SetParent(parent, false);
        var overlayRect = overlayObject.GetComponent<RectTransform>();
        Stretch(overlayRect);

        var dimmer = AddImage("Dimmer", overlayRect, new Color(0f, 0f, 0f, 0.72f));
        Stretch(dimmer.rectTransform);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(overlayRect, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(FollowerInventoryOverlayStyle.PanelWidth, FollowerInventoryOverlayStyle.PanelHeight);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 0.96f);

        var titleText = AddText("Title", panelRect, fontAsset, 28f, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleText.rectTransform.pivot = new Vector2(0f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(28f, -20f);
        titleText.rectTransform.sizeDelta = new Vector2(760f, 36f);

        var statusText = AddText("Status", panelRect, fontAsset, 18f, FontStyles.Normal);
        statusText.alignment = TextAlignmentOptions.Left;
        statusText.color = new Color(0.82f, 0.85f, 0.9f, 1f);
        statusText.rectTransform.anchorMin = new Vector2(0f, 1f);
        statusText.rectTransform.anchorMax = new Vector2(0f, 1f);
        statusText.rectTransform.pivot = new Vector2(0f, 1f);
        statusText.rectTransform.anchoredPosition = new Vector2(30f, -62f);
        statusText.rectTransform.sizeDelta = new Vector2(760f, 24f);

        var errorText = AddText("Error", panelRect, fontAsset, 18f, FontStyles.Bold);
        errorText.alignment = TextAlignmentOptions.Left;
        errorText.color = new Color(1f, 0.42f, 0.42f, 1f);
        errorText.rectTransform.anchorMin = new Vector2(0f, 1f);
        errorText.rectTransform.anchorMax = new Vector2(1f, 1f);
        errorText.rectTransform.pivot = new Vector2(0f, 1f);
        errorText.rectTransform.anchoredPosition = new Vector2(30f, -96f);
        errorText.rectTransform.sizeDelta = new Vector2(-60f, 46f);
        errorText.enableWordWrapping = true;

        var debugText = AddText("Debug", panelRect, fontAsset, 14f, FontStyles.Normal);
        debugText.alignment = TextAlignmentOptions.Left;
        debugText.color = new Color(0.86f, 0.89f, 0.95f, 0.92f);
        debugText.rectTransform.anchorMin = new Vector2(0f, 1f);
        debugText.rectTransform.anchorMax = new Vector2(1f, 1f);
        debugText.rectTransform.pivot = new Vector2(0f, 1f);
        debugText.rectTransform.anchoredPosition = new Vector2(30f, -138f);
        debugText.rectTransform.sizeDelta = new Vector2(-60f, 84f);
        debugText.enableWordWrapping = true;
        debugText.overflowMode = TextOverflowModes.Truncate;
        debugText.gameObject.SetActive(false);

        var actionDockObject = new GameObject("ActionDock", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        actionDockObject.transform.SetParent(panelRect, false);
        var actionDockRect = actionDockObject.GetComponent<RectTransform>();
        actionDockRect.anchorMin = new Vector2(1f, 1f);
        actionDockRect.anchorMax = new Vector2(1f, 1f);
        actionDockRect.pivot = new Vector2(1f, 1f);
        actionDockRect.anchoredPosition = new Vector2(-28f, -20f);
        actionDockRect.sizeDelta = new Vector2(FollowerInventoryOverlayStyle.ActionDockWidth, 0f);
        var actionDockLayout = actionDockObject.GetComponent<VerticalLayoutGroup>();
        actionDockLayout.spacing = FollowerInventoryOverlayStyle.TargetDockTopOffset;
        actionDockLayout.childControlWidth = true;
        actionDockLayout.childControlHeight = true;
        actionDockLayout.childForceExpandWidth = false;
        actionDockLayout.childForceExpandHeight = false;
        var actionDockFitter = actionDockObject.GetComponent<ContentSizeFitter>();
        actionDockFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        actionDockFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var actionsRowObject = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        actionsRowObject.transform.SetParent(actionDockRect, false);
        var actionsRowRect = actionsRowObject.GetComponent<RectTransform>();
        actionsRowRect.sizeDelta = new Vector2(FollowerInventoryOverlayStyle.ActionDockWidth, 0f);
        var actionsRowLayout = actionsRowObject.GetComponent<HorizontalLayoutGroup>();
        actionsRowLayout.spacing = 12f;
        actionsRowLayout.childControlWidth = false;
        actionsRowLayout.childControlHeight = false;
        actionsRowLayout.childForceExpandWidth = false;
        actionsRowLayout.childForceExpandHeight = false;
        var actionsRowFitter = actionsRowObject.GetComponent<ContentSizeFitter>();
        actionsRowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        actionsRowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var primaryAction = CreateDockButton(actionsRowRect, fontAsset, "PrimaryAction", "SELECT AN ITEM", new Vector2(260f, 42f));
        primaryAction.Button.onClick.AddListener(() => _ = actions.RunPrimaryActionAsync());

        var closeButton = CreateDockButton(actionsRowRect, fontAsset, "Close", "CLOSE", new Vector2(160f, 42f));
        closeButton.Button.onClick.AddListener(actions.Close.Invoke);

        var targetClusterObject = new GameObject("TargetCluster", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        targetClusterObject.transform.SetParent(actionDockRect, false);
        var targetClusterRect = targetClusterObject.GetComponent<RectTransform>();
        targetClusterRect.sizeDelta = new Vector2(FollowerInventoryOverlayStyle.ActionDockWidth, 0f);
        targetClusterObject.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.14f, 0.98f);
        var targetClusterLayout = targetClusterObject.GetComponent<VerticalLayoutGroup>();
        targetClusterLayout.spacing = 6f;
        targetClusterLayout.padding = new RectOffset(
            (int)FollowerInventoryOverlayStyle.TargetDockPadding,
            (int)FollowerInventoryOverlayStyle.TargetDockPadding,
            (int)FollowerInventoryOverlayStyle.TargetDockPadding,
            (int)FollowerInventoryOverlayStyle.TargetDockPadding);
        targetClusterLayout.childControlWidth = true;
        targetClusterLayout.childControlHeight = true;
        targetClusterLayout.childForceExpandWidth = true;
        targetClusterLayout.childForceExpandHeight = false;
        var targetClusterFitter = targetClusterObject.GetComponent<ContentSizeFitter>();
        targetClusterFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        targetClusterFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var targetHeader = AddText("TargetHeader", targetClusterRect, fontAsset, 12f, FontStyles.Bold);
        targetHeader.alignment = TextAlignmentOptions.Left;
        targetHeader.color = new Color(0.72f, 0.77f, 0.84f, 1f);
        targetHeader.text = "FOLLOWER TARGET";
        targetHeader.rectTransform.sizeDelta = new Vector2(FollowerInventoryOverlayStyle.ActionDockWidth - (FollowerInventoryOverlayStyle.TargetDockPadding * 2f), 16f);

        var targetButtonsObject = new GameObject("TargetButtons", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        targetButtonsObject.transform.SetParent(targetClusterRect, false);
        var targetButtonsRect = targetButtonsObject.GetComponent<RectTransform>();
        targetButtonsRect.sizeDelta = new Vector2(FollowerInventoryOverlayStyle.ActionDockWidth - (FollowerInventoryOverlayStyle.TargetDockPadding * 2f), 0f);
        var targetLayout = targetButtonsObject.GetComponent<GridLayoutGroup>();
        targetLayout.spacing = new Vector2(FollowerInventoryOverlayStyle.TargetClusterSpacing, FollowerInventoryOverlayStyle.TargetClusterSpacing);
        targetLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        targetLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        targetLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        targetLayout.constraintCount = FollowerInventoryOverlayStyle.TargetButtonsPerRow;
        targetLayout.cellSize = new Vector2(FollowerInventoryOverlayStyle.TargetChipWidth, FollowerInventoryOverlayStyle.TargetChipHeight);
        var targetFitter = targetButtonsObject.GetComponent<ContentSizeFitter>();
        targetFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        targetFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var followerColumn = CreateColumn(
            panelRect,
            fontAsset,
            "FOLLOWER",
            new Vector2(28f, FollowerInventoryOverlayStyle.ColumnsTopOffset),
            new Vector2(FollowerInventoryOverlayStyle.FollowerColumnWidth, FollowerInventoryOverlayStyle.ColumnHeight));
        var playerColumn = CreateColumn(
            panelRect,
            fontAsset,
            "PLAYER",
            new Vector2(28f + FollowerInventoryOverlayStyle.FollowerColumnWidth + 36f, FollowerInventoryOverlayStyle.ColumnsTopOffset),
            new Vector2(FollowerInventoryOverlayStyle.PlayerColumnWidth, FollowerInventoryOverlayStyle.ColumnHeight));
        AttachDropTarget(followerColumn.ColumnRoot.gameObject, actions, "player", null);
        AttachDropTarget(followerColumn.ScrollRoot.gameObject, actions, "player", null);
        AttachDropTarget(followerColumn.ItemsRoot.gameObject, actions, "player", null);
        AttachDropTarget(playerColumn.ColumnRoot.gameObject, actions, "follower", null);
        AttachDropTarget(playerColumn.ScrollRoot.gameObject, actions, "follower", null);
        AttachDropTarget(playerColumn.ItemsRoot.gameObject, actions, "follower", null);

        return new FollowerInventoryOverlayView(
            overlayObject,
            actions,
            titleText,
            statusText,
            errorText,
            debugText,
            primaryAction.Button,
            primaryAction.Label,
            targetClusterObject,
            targetButtonsRect,
            playerColumn.SummaryText,
            playerColumn.ItemsRoot,
            followerColumn.SummaryText,
            followerColumn.ItemsRoot,
            fontAsset);
    }

    public void Render(FollowerInventoryScreenViewModel model)
    {
        titleText.text = model.Title;
        statusText.text = model.StatusText;
        errorText.text = model.ErrorMessage ?? string.Empty;
        errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(model.ErrorMessage));
        debugText.text = model.DebugDetails ?? string.Empty;
        debugText.gameObject.SetActive(!string.IsNullOrWhiteSpace(model.DebugDetails));
        primaryActionLabel.text = model.PrimaryActionText ?? "SELECT AN ITEM";
        primaryActionButton.interactable = model.CanRunPrimaryAction;
        targetClusterObject.SetActive(model.AvailableTargets.Count > 0);
        RenderTargets(targetButtonsRoot, model.AvailableTargets);

        var playerSection = model.Sections.FirstOrDefault(section => string.Equals(section.Title, "Player", StringComparison.Ordinal));
        var followerSection = model.Sections.FirstOrDefault(section => string.Equals(section.Title, "Follower", StringComparison.Ordinal));

        playerSummaryText.text = playerSection?.Summary ?? "0 items";
        RenderItems(playerItemsRoot, playerSection?.Items ?? Array.Empty<FollowerInventoryScreenItemViewModel>());

        followerSummaryText.text = followerSection?.Summary ?? "0 items";
        RenderFollowerPane(followerItemsRoot, model.FollowerPane, followerSection?.Items ?? Array.Empty<FollowerInventoryScreenItemViewModel>());
    }

    public void Dispose()
    {
        if (rootObject is not null)
        {
            UnityEngine.Object.Destroy(rootObject);
        }
    }

    private void RenderTargets(RectTransform root, IReadOnlyList<FollowerInventoryScreenTargetViewModel> targets)
    {
        ClearChildren(root);

        if (targets.Count == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            var chip = CreateTargetChip(root, target);
            chip.Button.onClick.AddListener(() => actions.SelectTarget(target.Key));
            chip.Button.GetComponent<Image>().color = target.IsSelected
                ? (target.IsEquipTarget ? new Color(0.3f, 0.45f, 0.26f, 1f) : new Color(0.24f, 0.36f, 0.54f, 1f))
                : new Color(0.14f, 0.18f, 0.24f, 1f);
        }
    }

    private void RenderItems(RectTransform root, IReadOnlyList<FollowerInventoryScreenItemViewModel> items)
    {
        ClearChildren(root);

        if (items.Count == 0)
        {
            var emptyText = AddText("Empty", root, fontAsset, 16f, FontStyles.Italic);
            emptyText.alignment = TextAlignmentOptions.Left;
            emptyText.color = new Color(0.75f, 0.78f, 0.82f, 1f);
            emptyText.text = "No items.";
            emptyText.enableWordWrapping = true;
            emptyText.overflowMode = TextOverflowModes.Overflow;
            emptyText.rectTransform.sizeDelta = new Vector2(436f, 42f);
            return;
        }

        foreach (var item in items)
        {
            var entry = CreateInventoryItemEntry(root, item);
            entry.Button.onClick.AddListener(() => actions.SelectItem(item.Owner, item.Id));
            var isEquipped = item.SecondaryText.StartsWith("Equipped:", StringComparison.Ordinal);
            entry.Button.GetComponent<Image>().color = item.IsSelected
                ? new Color(0.28f, 0.44f, 0.62f, 1f)
                : isEquipped
                    ? new Color(0.18f, 0.24f, 0.18f, 1f)
                    : new Color(0.14f, 0.18f, 0.24f, 1f);
        }
    }

    private void RenderFollowerPane(
        RectTransform root,
        FollowerInventoryFollowerPaneViewModel? pane,
        IReadOnlyList<FollowerInventoryScreenItemViewModel> fallbackItems)
    {
        ClearChildren(root);
        if (pane is null)
        {
            RenderItems(root, fallbackItems);
            return;
        }

        RenderEquipmentSection(root, pane);

        foreach (var container in pane.Containers)
        {
            var section = CreateSectionBody(root, container.Label.ToUpperInvariant(), $"{container.Items.Count} {(container.Items.Count == 1 ? "item" : "items")}");
            var body = section.BodyRoot;
            AttachDropTarget(section.SectionRoot.gameObject, actions, "player", $"store:{container.ContainerItemId}");
            if (container.Items.Count == 0)
            {
                RenderEmptySection(body, "Empty.");
                continue;
            }

            foreach (var item in container.Items)
            {
                var entry = CreateInventoryItemEntry(body, item, item.Depth);
                entry.Button.onClick.AddListener(() => actions.SelectItem(item.Owner, item.Id));
                entry.Button.GetComponent<Image>().color = item.IsSelected
                    ? new Color(0.28f, 0.44f, 0.62f, 1f)
                    : new Color(0.14f, 0.18f, 0.24f, 1f);
            }
        }

        if (pane.OverflowItems.Count > 0)
        {
            var body = CreateSectionBody(root, "UNPLACED ITEMS", "Missing parent links or malformed ownership.").BodyRoot;
            foreach (var item in pane.OverflowItems)
            {
                var entry = CreateInventoryItemEntry(body, item, item.Depth);
                entry.Button.onClick.AddListener(() => actions.SelectItem(item.Owner, item.Id));
                entry.Button.GetComponent<Image>().color = item.IsSelected
                    ? new Color(0.45f, 0.34f, 0.24f, 1f)
                    : new Color(0.2f, 0.15f, 0.12f, 1f);
            }
        }
    }

    private void RenderEquipmentSection(RectTransform root, FollowerInventoryFollowerPaneViewModel pane)
    {
        var equippedCount = pane.EquipmentSlots.Count(slot => slot.Item is not null);
        var body = CreateSectionBody(root, "EQUIPPED", $"{equippedCount}/{pane.EquipmentSlots.Count} slots filled", true).BodyRoot;

        var rowsRoot = new GameObject("EquipmentRows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rowsRoot.transform.SetParent(body, false);
        var rowsRect = rowsRoot.GetComponent<RectTransform>();
        rowsRect.anchorMin = new Vector2(0f, 1f);
        rowsRect.anchorMax = new Vector2(1f, 1f);
        rowsRect.pivot = new Vector2(0.5f, 1f);
        rowsRect.sizeDelta = new Vector2(0f, 0f);
        var rowsLayout = rowsRoot.GetComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = FollowerInventoryOverlayStyle.SectionSpacing;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        rowsRoot.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (var index = 0; index < pane.EquipmentSlots.Count; index += FollowerInventoryOverlayStyle.FollowerEquipmentColumns)
        {
            var rowObject = new GameObject($"EquipmentRow-{index}", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            rowObject.transform.SetParent(rowsRect, false);
            var rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, 0f);
            var rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = FollowerInventoryOverlayStyle.SectionSpacing;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowObject.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rowSlots = pane.EquipmentSlots.Skip(index).Take(FollowerInventoryOverlayStyle.FollowerEquipmentColumns).ToArray();
            foreach (var slot in rowSlots)
            {
                CreateFollowerSlotCard(rowRect, slot);
            }

            for (var filler = rowSlots.Length; filler < FollowerInventoryOverlayStyle.FollowerEquipmentColumns; filler++)
            {
                CreateFollowerSlotFiller(rowRect);
            }
        }
    }

    private static RectTransform ResolveParent(object? hostScreen)
    {
        if (hostScreen is Component component)
        {
            var canvas = component.GetComponentInParent<Canvas>();
            if (canvas is not null && canvas.transform is RectTransform canvasRect)
            {
                return canvasRect;
            }

            if (component.transform is RectTransform componentRect)
            {
                return componentRect;
            }
        }

        var fallbackCanvas = UnityEngine.Object.FindObjectsOfType<Canvas>(true).FirstOrDefault();
        if (fallbackCanvas is not null && fallbackCanvas.transform is RectTransform fallbackRect)
        {
            return fallbackRect;
        }

        throw new InvalidOperationException("Failed to resolve inventory overlay parent canvas.");
    }

    private static (RectTransform ColumnRoot, RectTransform ScrollRoot, TextMeshProUGUI SummaryText, RectTransform ItemsRoot) CreateColumn(
        RectTransform parent,
        TMP_FontAsset? fontAsset,
        string title,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        var column = new GameObject($"{title}Column", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        column.transform.SetParent(parent, false);
        var columnRect = column.GetComponent<RectTransform>();
        columnRect.anchorMin = new Vector2(0f, 1f);
        columnRect.anchorMax = new Vector2(0f, 1f);
        columnRect.pivot = new Vector2(0f, 1f);
        columnRect.anchoredPosition = anchoredPosition;
        columnRect.sizeDelta = size;
        column.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.14f, 0.98f);

        var header = AddText("Header", columnRect, fontAsset, 22f, FontStyles.Bold);
        header.text = title;
        header.alignment = TextAlignmentOptions.Left;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(0f, 1f);
        header.rectTransform.pivot = new Vector2(0f, 1f);
        header.rectTransform.anchoredPosition = new Vector2(18f, -18f);
        header.rectTransform.sizeDelta = new Vector2(size.x - 36f, 30f);

        var summary = AddText("Summary", columnRect, fontAsset, 16f, FontStyles.Normal);
        summary.alignment = TextAlignmentOptions.Left;
        summary.color = new Color(0.78f, 0.82f, 0.88f, 1f);
        summary.rectTransform.anchorMin = new Vector2(0f, 1f);
        summary.rectTransform.anchorMax = new Vector2(0f, 1f);
        summary.rectTransform.pivot = new Vector2(0f, 1f);
        summary.rectTransform.anchoredPosition = new Vector2(18f, -52f);
        summary.rectTransform.sizeDelta = new Vector2(size.x - 36f, 24f);

        var scrollRoot = new GameObject("ScrollRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
        scrollRoot.transform.SetParent(columnRect, false);
        var scrollRootRect = scrollRoot.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0f, 0f);
        scrollRootRect.anchorMax = new Vector2(1f, 1f);
        scrollRootRect.offsetMin = new Vector2(16f, 16f);
        scrollRootRect.offsetMax = new Vector2(-16f, -88f);
        var scrollImage = scrollRoot.GetComponent<Image>();
        scrollImage.color = new Color(0.05f, 0.06f, 0.08f, 0.92f);
        scrollRoot.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollRootRect, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(12f, 0f);
        contentRect.offsetMax = new Vector2(-12f, 0f);
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = FollowerInventoryOverlayStyle.SectionSpacing;
        layout.padding = new RectOffset(0, 0, (int)FollowerInventoryOverlayStyle.ColumnContentTopPadding, 0);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = scrollRootRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = FollowerInventoryOverlayStyle.ScrollSensitivity;

        return (columnRect, scrollRootRect, summary, contentRect);
    }

    private (Button Button, TextMeshProUGUI Label) CreateInventoryItemEntry(
        Transform parent,
        FollowerInventoryScreenItemViewModel item,
        int depth = 0)
    {
        var buttonObject = new GameObject($"Item-{item.Id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, FollowerInventoryOverlayStyle.TreeRowHeight);
        var layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = FollowerInventoryOverlayStyle.TreeRowHeight;
        layout.flexibleWidth = 1f;
        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.14f, 0.18f, 0.24f, 1f);
        var button = buttonObject.GetComponent<Button>();
        AttachDragSource(buttonObject, actions, item.Owner, item.Id);

        var indent = depth * FollowerInventoryOverlayStyle.TreeIndentPerDepth;

        var iconFrame = new GameObject("IconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconFrame.transform.SetParent(rect, false);
        var iconFrameRect = iconFrame.GetComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0f, 0.5f);
        iconFrameRect.anchorMax = new Vector2(0f, 0.5f);
        iconFrameRect.pivot = new Vector2(0f, 0.5f);
        iconFrameRect.anchoredPosition = new Vector2(12f + indent, 0f);
        iconFrameRect.sizeDelta = new Vector2(52f, 52f);
        iconFrame.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.14f, 1f);

        var iconImageObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(FollowerInventoryItemIconView));
        iconImageObject.transform.SetParent(iconFrameRect, false);
        var iconImageRect = iconImageObject.GetComponent<RectTransform>();
        Stretch(iconImageRect);
        var iconImage = iconImageObject.GetComponent<Image>();
        iconImage.color = new Color(0.3f, 0.35f, 0.42f, 0.45f);
        iconImage.preserveAspect = true;
        iconImageObject.GetComponent<FollowerInventoryItemIconView>().Bind(item.Id, item.TemplateId);

        var primary = AddText("Primary", rect, fontAsset, 16f, FontStyles.Bold);
        primary.alignment = TextAlignmentOptions.TopLeft;
        primary.text = item.PrimaryText;
        primary.enableWordWrapping = false;
        primary.rectTransform.anchorMin = new Vector2(0f, 1f);
        primary.rectTransform.anchorMax = new Vector2(1f, 1f);
        primary.rectTransform.pivot = new Vector2(0f, 1f);
        primary.rectTransform.anchoredPosition = new Vector2(76f + indent, -10f);
        primary.rectTransform.sizeDelta = new Vector2(-(88f + indent), 24f);

        var secondary = AddText("Secondary", rect, fontAsset, 13f, FontStyles.Normal);
        secondary.alignment = TextAlignmentOptions.TopLeft;
        secondary.color = item.SecondaryText.StartsWith("Equipped:", StringComparison.Ordinal)
            ? new Color(0.84f, 0.92f, 0.74f, 1f)
            : new Color(0.78f, 0.82f, 0.88f, 1f);
        secondary.text = item.SecondaryText;
        secondary.enableWordWrapping = false;
        secondary.rectTransform.anchorMin = new Vector2(0f, 1f);
        secondary.rectTransform.anchorMax = new Vector2(1f, 1f);
        secondary.rectTransform.pivot = new Vector2(0f, 1f);
        secondary.rectTransform.anchoredPosition = new Vector2(76f + indent, -38f);
        secondary.rectTransform.sizeDelta = new Vector2(-(88f + indent), 18f);

        return (button, primary);
    }

    private void CreateFollowerSlotCard(Transform parent, FollowerInventoryFollowerSlotViewModel slot)
    {
        var cardObject = new GameObject($"Slot-{slot.SlotId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
        cardObject.transform.SetParent(parent, false);
        var rect = cardObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, FollowerInventoryOverlayStyle.SlotCardHeight);
        var layout = cardObject.GetComponent<LayoutElement>();
        layout.preferredHeight = FollowerInventoryOverlayStyle.SlotCardHeight;
        layout.flexibleWidth = 1f;
        var background = cardObject.GetComponent<Image>();
        background.color = slot.Item?.IsSelected == true
            ? new Color(0.28f, 0.44f, 0.62f, 1f)
            : slot.Item is not null
                ? new Color(0.16f, 0.18f, 0.13f, 1f)
                : new Color(0.11f, 0.13f, 0.16f, 1f);
        var outline = cardObject.GetComponent<Outline>();
        outline.effectDistance = new Vector2(
            FollowerInventoryOverlayStyle.BorderOutlineHorizontalPixels,
            -FollowerInventoryOverlayStyle.BorderOutlineVerticalPixels);
        outline.effectColor = slot.Item is null
            ? new Color(0.18f, 0.21f, 0.26f, 1f)
            : new Color(0.33f, 0.38f, 0.19f, 1f);

        var button = cardObject.AddComponent<Button>();
        button.interactable = slot.Item is not null;
        if (slot.Item is not null)
        {
            button.onClick.AddListener(() => actions.SelectItem(slot.Item.Owner, slot.Item.Id));
            AttachDragSource(cardObject, actions, slot.Item.Owner, slot.Item.Id);
            if (IsCarryContainerSlot(slot.SlotId))
            {
                AttachDropTarget(cardObject, actions, "player", $"store:{slot.Item.Id}");
            }
        }
        else
        {
            AttachDropTarget(cardObject, actions, "player", $"equip:{slot.SlotId}");
        }

        var slotLabel = AddText("SlotLabel", rect, fontAsset, 12f, FontStyles.Bold);
        slotLabel.alignment = TextAlignmentOptions.TopLeft;
        slotLabel.color = new Color(0.73f, 0.78f, 0.84f, 1f);
        slotLabel.text = slot.Label.ToUpperInvariant();
        slotLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        slotLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        slotLabel.rectTransform.pivot = new Vector2(0f, 1f);
        slotLabel.rectTransform.anchoredPosition = new Vector2(8f, -6f);
        slotLabel.rectTransform.sizeDelta = new Vector2(-16f, 14f);

        if (slot.Item is null)
        {
            var emptyLabel = AddText("EmptyLabel", rect, fontAsset, 14f, FontStyles.Italic);
            emptyLabel.alignment = TextAlignmentOptions.Center;
            emptyLabel.color = new Color(0.54f, 0.59f, 0.66f, 1f);
            emptyLabel.text = "EMPTY";
            emptyLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            emptyLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            emptyLabel.rectTransform.offsetMin = new Vector2(8f, 8f);
            emptyLabel.rectTransform.offsetMax = new Vector2(-8f, -20f);
            return;
        }

        var iconFrame = new GameObject("IconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconFrame.transform.SetParent(rect, false);
        var iconFrameRect = iconFrame.GetComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0f, 0.5f);
        iconFrameRect.anchorMax = new Vector2(0f, 0.5f);
        iconFrameRect.pivot = new Vector2(0f, 0.5f);
        iconFrameRect.anchoredPosition = new Vector2(8f, -6f);
        iconFrameRect.sizeDelta = new Vector2(40f, 40f);
        iconFrame.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.14f, 1f);

        var iconImageObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(FollowerInventoryItemIconView));
        iconImageObject.transform.SetParent(iconFrameRect, false);
        var iconImageRect = iconImageObject.GetComponent<RectTransform>();
        Stretch(iconImageRect);
        var iconImage = iconImageObject.GetComponent<Image>();
        iconImage.color = new Color(0.3f, 0.35f, 0.42f, 0.45f);
        iconImage.preserveAspect = true;
        iconImageObject.GetComponent<FollowerInventoryItemIconView>().Bind(slot.Item.Id, slot.Item.TemplateId);

        var primary = AddText("Primary", rect, fontAsset, 14f, FontStyles.Bold);
        primary.alignment = TextAlignmentOptions.TopLeft;
        primary.text = slot.Item.PrimaryText;
        primary.rectTransform.anchorMin = new Vector2(0f, 1f);
        primary.rectTransform.anchorMax = new Vector2(1f, 1f);
        primary.rectTransform.pivot = new Vector2(0f, 1f);
        primary.rectTransform.anchoredPosition = new Vector2(54f, -24f);
        primary.rectTransform.sizeDelta = new Vector2(-62f, 18f);

        var secondary = AddText("Secondary", rect, fontAsset, 12f, FontStyles.Normal);
        secondary.alignment = TextAlignmentOptions.TopLeft;
        secondary.color = new Color(0.78f, 0.82f, 0.88f, 1f);
        secondary.text = slot.Item.SecondaryText;
        secondary.rectTransform.anchorMin = new Vector2(0f, 1f);
        secondary.rectTransform.anchorMax = new Vector2(1f, 1f);
        secondary.rectTransform.pivot = new Vector2(0f, 1f);
        secondary.rectTransform.anchoredPosition = new Vector2(54f, -42f);
        secondary.rectTransform.sizeDelta = new Vector2(-62f, 16f);
    }

    private void CreateFollowerSlotFiller(Transform parent)
    {
        var filler = new GameObject("SlotFiller", typeof(RectTransform), typeof(LayoutElement));
        filler.transform.SetParent(parent, false);
        var layout = filler.GetComponent<LayoutElement>();
        layout.preferredHeight = FollowerInventoryOverlayStyle.SlotCardHeight;
        layout.flexibleWidth = 1f;
    }

    private (RectTransform SectionRoot, RectTransform BodyRoot) CreateSectionBody(Transform parent, string title, string? subtitle = null, bool isEquipmentSection = false)
    {
        var section = new GameObject($"{title}-Section", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        section.transform.SetParent(parent, false);
        var sectionRect = section.GetComponent<RectTransform>();
        sectionRect.anchorMin = new Vector2(0f, 1f);
        sectionRect.anchorMax = new Vector2(1f, 1f);
        sectionRect.pivot = new Vector2(0.5f, 1f);
        sectionRect.sizeDelta = new Vector2(0f, 0f);
        section.GetComponent<Image>().color = isEquipmentSection
            ? new Color(0.07f, 0.09f, 0.11f, 0.98f)
            : new Color(0.08f, 0.1f, 0.12f, 0.98f);
        var outline = section.GetComponent<Outline>();
        outline.effectDistance = new Vector2(
            FollowerInventoryOverlayStyle.BorderOutlineHorizontalPixels,
            -FollowerInventoryOverlayStyle.BorderOutlineVerticalPixels);
        outline.effectColor = isEquipmentSection
            ? new Color(0.25f, 0.29f, 0.18f, 1f)
            : new Color(0.17f, 0.2f, 0.24f, 1f);
        var layout = section.GetComponent<VerticalLayoutGroup>();
        layout.spacing = FollowerInventoryOverlayStyle.SectionSpacing;
        layout.padding = new RectOffset(
            (int)FollowerInventoryOverlayStyle.SectionPadding,
            (int)FollowerInventoryOverlayStyle.SectionPadding,
            (int)FollowerInventoryOverlayStyle.SectionPadding,
            (int)FollowerInventoryOverlayStyle.SectionPadding);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = section.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sectionLayout = section.GetComponent<LayoutElement>();
        sectionLayout.flexibleWidth = 1f;

        var titleText = AddText("SectionTitle", sectionRect, fontAsset, 14f, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = new Color(0.9f, 0.93f, 0.97f, 1f);
        titleText.text = title;
        titleText.enableWordWrapping = true;
        titleText.overflowMode = TextOverflowModes.Overflow;

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleText = AddText("SectionSubtitle", sectionRect, fontAsset, 12f, FontStyles.Normal);
            subtitleText.alignment = TextAlignmentOptions.Left;
            subtitleText.color = new Color(0.72f, 0.77f, 0.84f, 1f);
            subtitleText.text = subtitle;
            subtitleText.enableWordWrapping = true;
            subtitleText.overflowMode = TextOverflowModes.Overflow;
        }

        var bodyObject = new GameObject("SectionBody", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        bodyObject.transform.SetParent(sectionRect, false);
        var bodyRect = bodyObject.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.sizeDelta = new Vector2(0f, 0f);
        var bodyLayout = bodyObject.GetComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = FollowerInventoryOverlayStyle.SectionSpacing;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;
        bodyObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return (sectionRect, bodyRect);
    }

    private static bool IsCarryContainerSlot(string slotId)
    {
        return string.Equals(slotId, "Backpack", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "TacticalVest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "SecuredContainer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "Pockets", StringComparison.OrdinalIgnoreCase);
    }

    private static void AttachDragSource(GameObject gameObject, FollowerInventoryScreenActions actions, string owner, string itemId)
    {
        var dragSource = gameObject.GetComponent<FollowerInventoryDragSource>();
        if (dragSource is null)
        {
            dragSource = gameObject.AddComponent<FollowerInventoryDragSource>();
        }

        dragSource.Configure(owner, itemId, actions.LogDiagnostic);
    }

    private static void AttachDropTarget(GameObject gameObject, FollowerInventoryScreenActions actions, string acceptedSourceOwner, string? targetKey)
    {
        var dropTarget = gameObject.GetComponent<FollowerInventoryDropTarget>();
        if (dropTarget is null)
        {
            dropTarget = gameObject.AddComponent<FollowerInventoryDropTarget>();
        }

        dropTarget.Configure(actions, acceptedSourceOwner, targetKey, actions.LogDiagnostic);
    }

    private void RenderEmptySection(RectTransform root, string text)
    {
        var emptyText = AddText("Empty", root, fontAsset, 14f, FontStyles.Italic);
        emptyText.alignment = TextAlignmentOptions.Left;
        emptyText.color = new Color(0.75f, 0.78f, 0.82f, 1f);
        emptyText.text = text;
        emptyText.enableWordWrapping = true;
        emptyText.overflowMode = TextOverflowModes.Overflow;
    }

    private static void ClearChildren(Transform root)
    {
        for (var index = root.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.Destroy(root.GetChild(index).gameObject);
        }
    }

    private (Button Button, TextMeshProUGUI Label) CreateTargetChip(
        Transform parent,
        FollowerInventoryScreenTargetViewModel target)
    {
        var buttonObject = new GameObject($"Target-{target.Key}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(FollowerInventoryOverlayStyle.TargetChipWidth, FollowerInventoryOverlayStyle.TargetChipHeight);
        var layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = FollowerInventoryOverlayStyle.TargetChipHeight;
        layout.preferredWidth = FollowerInventoryOverlayStyle.TargetChipWidth;
        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.14f, 0.18f, 0.24f, 1f);
        var button = buttonObject.GetComponent<Button>();

        var text = AddText("Label", rect, fontAsset, 12f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.text = target.Label.ToUpperInvariant();
        Stretch(text.rectTransform);
        return (button, text);
    }

    private static (Button Button, TextMeshProUGUI Label) CreateDockButton(
        Transform parent,
        TMP_FontAsset? fontAsset,
        string name,
        string label,
        Vector2 size)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        var layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;
        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.24f, 0.32f, 1f);
        var button = buttonObject.GetComponent<Button>();

        var text = AddText("Label", rect, fontAsset, 16f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.text = label;
        Stretch(text.rectTransform);

        return (button, text);
    }

    private static Image AddImage(string name, RectTransform parent, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI AddText(string name, Transform parent, TMP_FontAsset? fontAsset, float fontSize, FontStyles fontStyle)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}

internal sealed class FollowerInventoryDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static FollowerInventoryDragPayload? CurrentPayload { get; private set; }

    private string owner = string.Empty;
    private string itemId = string.Empty;
    private Action<string>? logDiagnostic;

    public void Configure(string owner, string itemId, Action<string>? logDiagnostic)
    {
        this.owner = owner ?? string.Empty;
        this.itemId = itemId ?? string.Empty;
        this.logDiagnostic = logDiagnostic;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(itemId))
        {
            CurrentPayload = null;
            logDiagnostic?.Invoke($"Drag begin ignored: owner={owner}, item={itemId}, reason=Missing owner or item id.");
            return;
        }

        CurrentPayload = new FollowerInventoryDragPayload(owner, itemId);
        logDiagnostic?.Invoke($"Drag begin: owner={owner}, item={itemId}");
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        logDiagnostic?.Invoke(
            $"Drag end: owner={owner}, item={itemId}, pointerTarget={eventData.pointerCurrentRaycast.gameObject?.name ?? "<none>"}");
        CurrentPayload = null;
    }
}

internal sealed record FollowerInventoryDragPayload(string Owner, string ItemId);

internal sealed class FollowerInventoryDropTarget : MonoBehaviour, IDropHandler
{
    private FollowerInventoryScreenActions? actions;
    private string acceptedSourceOwner = string.Empty;
    private string? targetKey;
    private Action<string>? logDiagnostic;

    public void Configure(FollowerInventoryScreenActions actions, string acceptedSourceOwner, string? targetKey, Action<string>? logDiagnostic)
    {
        this.actions = actions;
        this.acceptedSourceOwner = acceptedSourceOwner ?? string.Empty;
        this.targetKey = targetKey;
        this.logDiagnostic = logDiagnostic;
    }

    public void OnDrop(PointerEventData eventData)
    {
        var payload = FollowerInventoryDragSource.CurrentPayload;
        if (payload is null
            || actions is null
            || string.IsNullOrWhiteSpace(payload.Owner)
            || string.IsNullOrWhiteSpace(payload.ItemId)
            || !string.Equals(payload.Owner, acceptedSourceOwner, StringComparison.OrdinalIgnoreCase))
        {
            logDiagnostic?.Invoke(
                $"Drop ignored: acceptedOwner={acceptedSourceOwner}, target={targetKey ?? "<player-stash>"}, payloadOwner={payload?.Owner ?? "<none>"}, payloadItem={payload?.ItemId ?? "<none>"}");
            return;
        }

        logDiagnostic?.Invoke(
            $"Drop accepted: acceptedOwner={acceptedSourceOwner}, target={targetKey ?? "<player-stash>"}, payloadOwner={payload.Owner}, payloadItem={payload.ItemId}");
        _ = RunTransferAsync(payload);
    }

    private async Task RunTransferAsync(FollowerInventoryDragPayload payload)
    {
        try
        {
            var result = await actions!.RunDropTransferAsync(payload.Owner, payload.ItemId, targetKey);
            logDiagnostic?.Invoke(
                $"Drop transfer completed: acceptedOwner={acceptedSourceOwner}, target={targetKey ?? "<player-stash>"}, payloadOwner={payload.Owner}, payloadItem={payload.ItemId}, succeeded={result.Succeeded}, error={result.ErrorMessage ?? "<none>"}");
        }
        catch (Exception ex)
        {
            logDiagnostic?.Invoke(
                $"Drop transfer faulted: acceptedOwner={acceptedSourceOwner}, target={targetKey ?? "<player-stash>"}, payloadOwner={payload.Owner}, payloadItem={payload.ItemId}, exception={ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}
#endif

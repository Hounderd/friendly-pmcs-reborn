using System.Text.Json;
using FriendlyPMC.Server.Models;
using FriendlyPMC.Server.Models.Requests;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace FriendlyPMC.Server.Services;

public static class FollowerInventoryValidationPolicy
{
    private static readonly string[] AutoFollowerTargetSlotPriority =
    [
        "Backpack",
        "TacticalVest",
        "SecuredContainer",
        "Pockets",
    ];

    private static readonly HashSet<string> AllowedFollowerRootSlots =
    [
        "Headwear",
        "Earpiece",
        "Eyewear",
        "FaceCover",
        "ArmorVest",
        "TacticalVest",
        "Backpack",
        "Pockets",
        "SecuredContainer",
        "FirstPrimaryWeapon",
        "SecondPrimaryWeapon",
        "Holster",
        "Scabbard",
        "ArmBand",
    ];

    public static string? ValidateTarget(
        IReadOnlyList<Item> targetItems,
        string equipmentRootId,
        IReadOnlyDictionary<string, TemplateItem> templates,
        FollowerInventoryMoveRequest request,
        Item movedItem,
        bool targetIsFollower)
    {
        var targetParent = targetItems.FirstOrDefault(item => string.Equals(item.Id.ToString(), request.ToId, StringComparison.Ordinal));
        if (targetParent is null)
        {
            return "Target container was not found.";
        }

        if (!templates.TryGetValue(targetParent.Template.ToString(), out var parentTemplate))
        {
            return "Target container template was not found.";
        }

        if (targetIsFollower && !IsFollowerCarryLegalTarget(targetItems, equipmentRootId, request.ToId, request.ToContainer))
        {
            return "Follower inventory target is not carry-legal.";
        }

        if (string.IsNullOrWhiteSpace(request.ToLocationJson))
        {
            if (targetIsFollower
                && !string.Equals(request.ToId, equipmentRootId, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(request.ToContainer)
                && EnumerateGridKeys(parentTemplate).Any())
            {
                return "No space available in target container.";
            }

            return HasSlot(parentTemplate, request.ToContainer)
                ? null
                : "Target slot was not found.";
        }

        if (!TryGetGrid(parentTemplate, request.ToContainer, out var grid))
        {
            return "Target grid was not found.";
        }

        if (!TryParseGridPlacement(request.ToLocationJson, out var requestedPlacement))
        {
            return "Target location was invalid.";
        }

        var movedSize = GetItemSize(templates, movedItem);
        if (requestedPlacement.X < 0
            || requestedPlacement.Y < 0
            || requestedPlacement.X + movedSize.Width > (grid.Properties?.CellsH ?? 0)
            || requestedPlacement.Y + movedSize.Height > (grid.Properties?.CellsV ?? 0))
        {
            return "No space available in target container.";
        }

        var occupyingChildren = targetItems
            .Where(item =>
                string.Equals(item.ParentId, request.ToId, StringComparison.Ordinal)
                && string.Equals(item.SlotId, request.ToContainer, StringComparison.Ordinal))
            .ToArray();
        foreach (var child in occupyingChildren)
        {
            if (!TryGetGridPlacement(child.Location, out var placement))
            {
                continue;
            }

            var childSize = GetItemSize(templates, child);
            if (RectanglesOverlap(requestedPlacement, movedSize, placement, childSize))
            {
                return "No space available in target container.";
            }
        }

        return null;
    }

    public static FollowerInventoryMoveRequest ResolveAutoPlacement(
        IReadOnlyList<Item> targetItems,
        string equipmentRootId,
        IReadOnlyDictionary<string, TemplateItem> templates,
        FollowerInventoryMoveRequest request,
        Item movedItem,
        bool targetIsFollower)
    {
        if (!string.IsNullOrWhiteSpace(request.ToLocationJson))
        {
            return request;
        }

        if (targetIsFollower
            && !string.IsNullOrWhiteSpace(request.ToId)
            && string.IsNullOrWhiteSpace(request.ToContainer)
            && TryResolveSpecifiedFollowerContainerTarget(targetItems, equipmentRootId, templates, request, movedItem, out var explicitTargetRequest))
        {
            request = explicitTargetRequest;
        }

        if (targetIsFollower
            && string.IsNullOrWhiteSpace(request.ToId)
            && TryResolveAutoFollowerGridTarget(targetItems, equipmentRootId, templates, request, movedItem, out var autoTargetRequest))
        {
            request = autoTargetRequest;
        }

        var targetParent = targetItems.FirstOrDefault(item => string.Equals(item.Id.ToString(), request.ToId, StringComparison.Ordinal));
        if (targetParent is null)
        {
            return request;
        }

        if (!templates.TryGetValue(targetParent.Template.ToString(), out var parentTemplate))
        {
            return request;
        }

        if (targetIsFollower && !IsFollowerCarryLegalTarget(targetItems, equipmentRootId, request.ToId, request.ToContainer))
        {
            return request;
        }

        if (HasSlot(parentTemplate, request.ToContainer))
        {
            return request;
        }

        if (!TryGetGrid(parentTemplate, request.ToContainer, out var grid))
        {
            return request;
        }

        if (!TryFindFirstFreeGridPlacement(targetItems, templates, request.ToId, request.ToContainer, grid, movedItem, out var placement))
        {
            return request;
        }

        return request with
        {
            ToLocationJson = JsonSerializer.Serialize(new
            {
                x = placement.X,
                y = placement.Y,
                r = placement.IsVertical ? "Vertical" : "Horizontal",
            }),
        };
    }

    private static bool TryResolveSpecifiedFollowerContainerTarget(
        IReadOnlyList<Item> targetItems,
        string equipmentRootId,
        IReadOnlyDictionary<string, TemplateItem> templates,
        FollowerInventoryMoveRequest request,
        Item movedItem,
        out FollowerInventoryMoveRequest resolvedRequest)
    {
        var targetContainer = targetItems.FirstOrDefault(item => string.Equals(item.Id.ToString(), request.ToId, StringComparison.Ordinal));
        if (targetContainer is null
            || !IsFollowerCarryLegalTarget(targetItems, equipmentRootId, targetContainer.Id.ToString(), "main")
            || !templates.TryGetValue(targetContainer.Template.ToString(), out var targetTemplate))
        {
            resolvedRequest = request;
            return false;
        }
        return TryResolveContainerGridTarget(targetItems, templates, request, movedItem, targetContainer, targetTemplate, out resolvedRequest);
    }

    private static bool TryResolveAutoFollowerGridTarget(
        IReadOnlyList<Item> targetItems,
        string equipmentRootId,
        IReadOnlyDictionary<string, TemplateItem> templates,
        FollowerInventoryMoveRequest request,
        Item movedItem,
        out FollowerInventoryMoveRequest resolvedRequest)
    {
        foreach (var preferredSlot in AutoFollowerTargetSlotPriority)
        {
            var targetContainer = targetItems.FirstOrDefault(item =>
                string.Equals(item.ParentId, equipmentRootId, StringComparison.Ordinal)
                && string.Equals(item.SlotId, preferredSlot, StringComparison.Ordinal));
            if (targetContainer is null)
            {
                continue;
            }

            if (!templates.TryGetValue(targetContainer.Template.ToString(), out var targetTemplate))
            {
                continue;
            }

            if (TryResolveContainerGridTarget(targetItems, templates, request, movedItem, targetContainer, targetTemplate, out resolvedRequest))
            {
                return true;
            }
        }

        resolvedRequest = request;
        return false;
    }

    private static bool TryResolveContainerGridTarget(
        IReadOnlyList<Item> targetItems,
        IReadOnlyDictionary<string, TemplateItem> templates,
        FollowerInventoryMoveRequest request,
        Item movedItem,
        Item targetContainer,
        TemplateItem targetTemplate,
        out FollowerInventoryMoveRequest resolvedRequest)
    {
        foreach (var gridKey in EnumerateGridKeys(targetTemplate))
        {
            if (!TryGetGrid(targetTemplate, gridKey, out var grid))
            {
                continue;
            }

            if (!TryFindFirstFreeGridPlacement(targetItems, templates, targetContainer.Id.ToString(), gridKey, grid, movedItem, out var placement))
            {
                continue;
            }

            resolvedRequest = request with
            {
                ToId = targetContainer.Id.ToString(),
                ToContainer = gridKey,
                ToLocationJson = JsonSerializer.Serialize(new
                {
                    x = placement.X,
                    y = placement.Y,
                    r = placement.IsVertical ? "Vertical" : "Horizontal",
                }),
            };
            return true;
        }

        resolvedRequest = request;
        return false;
    }

    private static bool IsFollowerCarryLegalTarget(
        IReadOnlyList<Item> targetItems,
        string equipmentRootId,
        string targetParentId,
        string targetContainer)
    {
        if (string.Equals(targetParentId, equipmentRootId, StringComparison.Ordinal))
        {
            return AllowedFollowerRootSlots.Contains(targetContainer);
        }

        var itemsById = targetItems.ToDictionary(item => item.Id.ToString(), StringComparer.Ordinal);
        var currentId = targetParentId;
        while (itemsById.TryGetValue(currentId, out var current))
        {
            if (string.Equals(current.ParentId, equipmentRootId, StringComparison.Ordinal))
            {
                return current.SlotId is not null && AllowedFollowerRootSlots.Contains(current.SlotId);
            }

            if (string.IsNullOrWhiteSpace(current.ParentId))
            {
                break;
            }

            currentId = current.ParentId;
        }

        return false;
    }

    private static bool HasSlot(TemplateItem template, string slotId)
    {
        return template.Properties?.Slots?.Any(slot => string.Equals(slot.Name, slotId, StringComparison.Ordinal)) == true;
    }

    private static bool TryGetGrid(TemplateItem template, string gridName, out Grid grid)
    {
        grid = template.Properties?.Grids?.FirstOrDefault(existing =>
                string.Equals(existing.Name, gridName, StringComparison.Ordinal)
                || string.Equals(existing.Id, gridName, StringComparison.Ordinal))
            ?? new Grid();
        return !string.IsNullOrWhiteSpace(grid.Name) || !string.IsNullOrWhiteSpace(grid.Id);
    }

    private static IEnumerable<string> EnumerateGridKeys(TemplateItem template)
    {
        return template.Properties?.Grids?
            .Select(grid => !string.IsNullOrWhiteSpace(grid.Name) ? grid.Name : grid.Id)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            ?? Enumerable.Empty<string>();
    }

    private static (int Width, int Height) GetItemSize(IReadOnlyDictionary<string, TemplateItem> templates, Item item)
    {
        if (!templates.TryGetValue(item.Template.ToString(), out var template))
        {
            return (1, 1);
        }

        var width = Math.Max(template.Properties?.Width ?? 1, 1);
        var height = Math.Max(template.Properties?.Height ?? 1, 1);
        if (TryGetGridPlacement(item.Location, out var placement) && placement.IsVertical)
        {
            return (height, width);
        }

        return (width, height);
    }

    private static bool TryParseGridPlacement(string? locationJson, out GridPlacement placement)
    {
        if (string.IsNullOrWhiteSpace(locationJson))
        {
            placement = default;
            return false;
        }

        using var document = JsonDocument.Parse(locationJson);
        return TryReadGridPlacement(document.RootElement, out placement);
    }

    private static bool TryGetGridPlacement(object? location, out GridPlacement placement)
    {
        switch (location)
        {
            case null:
                placement = default;
                return false;
            case JsonElement jsonElement:
                return TryReadGridPlacement(jsonElement, out placement);
            default:
                using (var document = JsonDocument.Parse(JsonSerializer.Serialize(location)))
                {
                    return TryReadGridPlacement(document.RootElement, out placement);
                }
        }
    }

    private static bool TryReadGridPlacement(JsonElement element, out GridPlacement placement)
    {
        placement = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetInt(element, "x", out var x) || !TryGetInt(element, "y", out var y))
        {
            return false;
        }

        var rotation = element.TryGetProperty("r", out var rotationElement)
            ? rotationElement.GetString()
            : element.TryGetProperty("rotation", out rotationElement)
                ? rotationElement.GetString()
                : null;
        placement = new GridPlacement(x, y, string.Equals(rotation, "Vertical", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt32(out value);
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    private static bool RectanglesOverlap(GridPlacement requested, (int Width, int Height) requestedSize, GridPlacement existing, (int Width, int Height) existingSize)
    {
        return requested.X < existing.X + existingSize.Width
            && requested.X + requestedSize.Width > existing.X
            && requested.Y < existing.Y + existingSize.Height
            && requested.Y + requestedSize.Height > existing.Y;
    }

    private static bool TryFindFirstFreeGridPlacement(
        IReadOnlyList<Item> targetItems,
        IReadOnlyDictionary<string, TemplateItem> templates,
        string targetParentId,
        string targetContainer,
        Grid grid,
        Item movedItem,
        out GridPlacement placement)
    {
        placement = default;
        var movedSize = GetItemSize(templates, movedItem);
        var widthLimit = Math.Max((grid.Properties?.CellsH ?? 0) - movedSize.Width, -1);
        var heightLimit = Math.Max((grid.Properties?.CellsV ?? 0) - movedSize.Height, -1);
        if (widthLimit < 0 || heightLimit < 0)
        {
            return false;
        }

        var occupyingChildren = targetItems
            .Where(item =>
                string.Equals(item.ParentId, targetParentId, StringComparison.Ordinal)
                && string.Equals(item.SlotId, targetContainer, StringComparison.Ordinal))
            .ToArray();

        for (var y = 0; y <= heightLimit; y++)
        {
            for (var x = 0; x <= widthLimit; x++)
            {
                var candidate = new GridPlacement(x, y, false);
                var blocked = occupyingChildren.Any(child =>
                {
                    if (!TryGetGridPlacement(child.Location, out var existingPlacement))
                    {
                        return false;
                    }

                    var childSize = GetItemSize(templates, child);
                    return RectanglesOverlap(candidate, movedSize, existingPlacement, childSize);
                });

                if (!blocked)
                {
                    placement = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private readonly record struct GridPlacement(int X, int Y, bool IsVertical);
}

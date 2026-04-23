using FriendlyPMC.Server.Models;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json;

namespace FriendlyPMC.Server.Services;

internal static class FollowerInventoryIdIntegrityPolicy
{
    public static bool NormalizePlayerProfileInventory(PmcData playerProfile)
    {
        return NormalizePlayerProfileInventory(playerProfile, null);
    }

    public static bool NormalizePlayerProfileInventory(
        PmcData playerProfile,
        IReadOnlyDictionary<string, TemplateItem>? templates)
    {
        ArgumentNullException.ThrowIfNull(playerProfile);

        if (playerProfile.Inventory?.Items is null)
        {
            return false;
        }

        var (normalizedItems, changed) = NormalizePlayerItems(playerProfile.Inventory.Items);
        if (templates is not null && NormalizeStructuredGridContainerLocations(normalizedItems, templates))
        {
            changed = true;
        }

        if (changed)
        {
            playerProfile.Inventory.Items = normalizedItems;
        }

        return changed;
    }

    public static (List<Item> Items, bool Changed) NormalizePlayerItems(IReadOnlyList<Item> items)
    {
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var latestAssignedIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var normalizedItems = new List<Item>(items.Count);
        var changed = false;

        foreach (var item in items)
        {
            var originalId = item.Id.ToString();
            var normalizedId = ReserveOrRemapId(originalId, usedIds, ref changed);
            latestAssignedIds[originalId] = normalizedId;

            var normalizedParentId = RemapParentId(item.ParentId, latestAssignedIds, ref changed);
            normalizedItems.Add(new Item
            {
                Id = new MongoId(normalizedId),
                Template = new MongoId(item.Template.ToString()),
                ParentId = normalizedParentId,
                SlotId = item.SlotId,
                Location = FollowerProfileFactory.DeserializeLocation(SerializeOptionalJson(item.Location)),
                Desc = item.Desc,
                Upd = item.Upd is null
                    ? null
                    : FollowerProfileFactory.DeserializeUpd(SerializeOptionalJson(item.Upd)),
            });
        }

        return (normalizedItems, changed);
    }

    public static (FollowerProfileSnapshot Profile, bool Changed) NormalizeFollowerProfile(
        FollowerProfileSnapshot profile,
        ISet<string>? reservedIds = null)
    {
        var inventory = profile.Inventory ?? FollowerInventoryMigrationPolicy.CreateInventorySnapshot(profile.Equipment);
        if (inventory is null)
        {
            return (profile, false);
        }

        var usedIds = reservedIds is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(reservedIds, StringComparer.Ordinal);
        var latestAssignedIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var changed = false;
        var normalizedItems = new List<FollowerInventoryItemSnapshot>(inventory.Items.Count);

        foreach (var item in inventory.Items)
        {
            var normalizedId = ReserveOrRemapId(item.Id, usedIds, ref changed);
            latestAssignedIds[item.Id] = normalizedId;

            var normalizedParentId = RemapParentId(item.ParentId, latestAssignedIds, ref changed);
            normalizedItems.Add(item with
            {
                Id = normalizedId,
                ParentId = normalizedParentId,
            });
        }

        var normalizedEquipmentId = latestAssignedIds.TryGetValue(inventory.EquipmentId, out var remappedEquipmentId)
            ? remappedEquipmentId
            : ReserveOrRemapId(inventory.EquipmentId, usedIds, ref changed);

        if (!string.Equals(normalizedEquipmentId, inventory.EquipmentId, StringComparison.Ordinal))
        {
            changed = true;
        }

        if (!changed)
        {
            return (profile, false);
        }

        var normalizedInventory = new FollowerInventorySnapshot(normalizedEquipmentId, normalizedItems);
        return (profile with
        {
            Inventory = normalizedInventory,
            Equipment = normalizedInventory.ToEquipmentSnapshot(),
        }, true);
    }

    public static (List<FollowerProfileSnapshot> Profiles, bool Changed) NormalizeFollowerProfiles(
        IReadOnlyList<FollowerProfileSnapshot> profiles)
    {
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var normalizedProfiles = new List<FollowerProfileSnapshot>(profiles.Count);
        var changed = false;

        foreach (var profile in profiles)
        {
            var (normalizedProfile, profileChanged) = NormalizeFollowerProfile(profile, usedIds);
            changed |= profileChanged;
            normalizedProfiles.Add(normalizedProfile);

            var normalizedInventory = normalizedProfile.Inventory ?? FollowerInventoryMigrationPolicy.CreateInventorySnapshot(normalizedProfile.Equipment);
            if (normalizedInventory is null)
            {
                continue;
            }

            usedIds.Add(normalizedInventory.EquipmentId);
            foreach (var item in normalizedInventory.Items)
            {
                usedIds.Add(item.Id);
            }
        }

        return (normalizedProfiles, changed);
    }

    private static string ReserveOrRemapId(string originalId, ISet<string> usedIds, ref bool changed)
    {
        if (usedIds.Add(originalId))
        {
            return originalId;
        }

        changed = true;
        string remappedId;
        do
        {
            remappedId = new MongoId().ToString();
        }
        while (!usedIds.Add(remappedId));

        return remappedId;
    }

    private static string? RemapParentId(
        string? originalParentId,
        IReadOnlyDictionary<string, string> latestAssignedIds,
        ref bool changed)
    {
        if (string.IsNullOrWhiteSpace(originalParentId))
        {
            return originalParentId;
        }

        if (!latestAssignedIds.TryGetValue(originalParentId, out var remappedParentId))
        {
            return originalParentId;
        }

        if (!string.Equals(remappedParentId, originalParentId, StringComparison.Ordinal))
        {
            changed = true;
        }

        return remappedParentId;
    }

    private static string? SerializeOptionalJson(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(value);
    }

    private static bool NormalizeStructuredGridContainerLocations(
        List<Item> items,
        IReadOnlyDictionary<string, TemplateItem> templates)
    {
        var itemsById = items.ToDictionary(item => item.Id.ToString(), StringComparer.Ordinal);
        var changed = false;
        var siblingGroups = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId) && !string.IsNullOrWhiteSpace(item.SlotId))
            .GroupBy(item => $"{item.ParentId}\n{item.SlotId}", StringComparer.Ordinal);

        foreach (var siblingGroup in siblingGroups)
        {
            var groupItems = siblingGroup.ToArray();
            if (groupItems.Length == 0)
            {
                continue;
            }

            var parentId = groupItems[0].ParentId;
            var slotId = groupItems[0].SlotId;
            if (string.IsNullOrWhiteSpace(parentId)
                || string.IsNullOrWhiteSpace(slotId)
                || !itemsById.TryGetValue(parentId, out var parentItem)
                || !templates.TryGetValue(parentItem.Template.ToString(), out var parentTemplate)
                || !TryGetGrid(parentTemplate, slotId, out var grid))
            {
                continue;
            }

            if (TryNormalizeGridGroup(groupItems, templates, grid, out var updatedLocations))
            {
                foreach (var update in updatedLocations)
                {
                    if (itemsById.TryGetValue(update.Key, out var item))
                    {
                        item.Location = update.Value;
                    }
                }

                changed = true;
            }
        }

        return changed;
    }

    private static bool TryNormalizeGridGroup(
        IReadOnlyList<Item> groupItems,
        IReadOnlyDictionary<string, TemplateItem> templates,
        Grid grid,
        out Dictionary<string, object> updatedLocations)
    {
        updatedLocations = new Dictionary<string, object>(StringComparer.Ordinal);
        var gridWidth = grid.Properties?.CellsH ?? 0;
        var gridHeight = grid.Properties?.CellsV ?? 0;
        if (gridWidth <= 0 || gridHeight <= 0)
        {
            return false;
        }

        var entries = groupItems
            .Select((item, order) => new GridLocationEntry(
                item,
                order,
                TryReadIndexedLocation(item.Location),
                TryGetGridPlacement(item.Location, out var placement) ? placement : null,
                GetItemSize(templates, item)))
            .ToArray();
        if (entries.All(entry => entry.StructuredPlacement is not null)
            && StructuredPlacementsAreValid(entries, gridWidth, gridHeight))
        {
            return false;
        }

        var occupied = new List<PlacedRect>();
        var needsRepair = false;

        foreach (var entry in entries
            .Where(entry => entry.StructuredPlacement is not null)
            .OrderBy(entry => entry.StructuredPlacement!.Value.Y)
            .ThenBy(entry => entry.StructuredPlacement!.Value.X)
            .ThenBy(entry => entry.Order))
        {
            var placement = entry.StructuredPlacement!.Value;
            var placedSize = GetPlacementSize(entry.Size, placement.IsVertical);
            if (!PlacementFits(placement, placedSize, gridWidth, gridHeight)
                || occupied.Any(existing => RectanglesOverlap(existing.Placement, existing.Size, placement, placedSize)))
            {
                needsRepair = true;
                continue;
            }

            occupied.Add(new PlacedRect(entry.Item.Id.ToString(), placement, placedSize));
        }

        var pending = entries
            .Where(entry => occupied.All(existing => !string.Equals(existing.ItemId, entry.Item.Id.ToString(), StringComparison.Ordinal)))
            .OrderByDescending(entry => entry.Size.Width * entry.Size.Height)
            .ThenByDescending(entry => Math.Max(entry.Size.Width, entry.Size.Height))
            .ThenByDescending(entry => Math.Min(entry.Size.Width, entry.Size.Height))
            .ThenBy(entry => entry.Index ?? int.MaxValue)
            .ThenBy(entry => entry.Order)
            .ToArray();

        if (!TryAssignGridPlacements(pending, occupied, gridWidth, gridHeight, updatedLocations))
        {
            updatedLocations.Clear();
            return false;
        }

        needsRepair = needsRepair || updatedLocations.Count > 0;
        return needsRepair && updatedLocations.Count > 0;
    }

    private static bool StructuredPlacementsAreValid(IReadOnlyList<GridLocationEntry> entries, int gridWidth, int gridHeight)
    {
        var occupied = new List<PlacedRect>(entries.Count);
        foreach (var entry in entries.OrderBy(candidate => candidate.Order))
        {
            var placement = entry.StructuredPlacement!.Value;
            var placedSize = GetPlacementSize(entry.Size, placement.IsVertical);
            if (!PlacementFits(placement, placedSize, gridWidth, gridHeight)
                || occupied.Any(existing => RectanglesOverlap(existing.Placement, existing.Size, placement, placedSize)))
            {
                return false;
            }

            occupied.Add(new PlacedRect(entry.Item.Id.ToString(), placement, placedSize));
        }

        return true;
    }

    private static bool TryAssignGridPlacements(
        IReadOnlyList<GridLocationEntry> pending,
        List<PlacedRect> occupied,
        int gridWidth,
        int gridHeight,
        Dictionary<string, object> updatedLocations)
    {
        if (pending.Count == 0)
        {
            return true;
        }

        return TryAssignGridPlacementsRecursive(pending, 0, occupied, gridWidth, gridHeight, updatedLocations);
    }

    private static bool TryAssignGridPlacementsRecursive(
        IReadOnlyList<GridLocationEntry> pending,
        int index,
        List<PlacedRect> occupied,
        int gridWidth,
        int gridHeight,
        Dictionary<string, object> updatedLocations)
    {
        if (index >= pending.Count)
        {
            return true;
        }

        var entry = pending[index];
        foreach (var placement in EnumerateCandidateGridPlacements(occupied, gridWidth, gridHeight, entry.Size))
        {
            var placedSize = GetPlacementSize(entry.Size, placement.IsVertical);
            occupied.Add(new PlacedRect(entry.Item.Id.ToString(), placement, placedSize));
            updatedLocations[entry.Item.Id.ToString()] = CreateGridLocation(placement);

            if (TryAssignGridPlacementsRecursive(pending, index + 1, occupied, gridWidth, gridHeight, updatedLocations))
            {
                return true;
            }

            updatedLocations.Remove(entry.Item.Id.ToString());
            occupied.RemoveAt(occupied.Count - 1);
        }

        return false;
    }

    private static IEnumerable<GridPlacement> EnumerateCandidateGridPlacements(
        IReadOnlyList<PlacedRect> occupied,
        int gridWidth,
        int gridHeight,
        (int Width, int Height) size)
    {
        foreach (var vertical in GetOrientationOrder(size))
        {
            var placedSize = GetPlacementSize(size, vertical);
            var widthLimit = gridWidth - placedSize.Width;
            var heightLimit = gridHeight - placedSize.Height;
            if (widthLimit < 0 || heightLimit < 0)
            {
                continue;
            }

            for (var y = 0; y <= heightLimit; y++)
            {
                for (var x = 0; x <= widthLimit; x++)
                {
                    var candidate = new GridPlacement(x, y, vertical);
                    if (occupied.Any(existing => RectanglesOverlap(existing.Placement, existing.Size, candidate, placedSize)))
                    {
                        continue;
                    }

                    yield return candidate;
                }
            }
        }
    }

    private static bool PlacementFits(GridPlacement placement, (int Width, int Height) size, int gridWidth, int gridHeight)
    {
        return placement.X >= 0
            && placement.Y >= 0
            && placement.X + size.Width <= gridWidth
            && placement.Y + size.Height <= gridHeight;
    }

    private static bool RectanglesOverlap(
        GridPlacement first,
        (int Width, int Height) firstSize,
        GridPlacement second,
        (int Width, int Height) secondSize)
    {
        return first.X < second.X + secondSize.Width
            && first.X + firstSize.Width > second.X
            && first.Y < second.Y + secondSize.Height
            && first.Y + firstSize.Height > second.Y;
    }

    private static IEnumerable<bool> GetOrientationOrder((int Width, int Height) size)
    {
        yield return false;

        if (size.Width != size.Height)
        {
            yield return true;
        }
    }

    private static (int Width, int Height) GetPlacementSize((int Width, int Height) size, bool isVertical)
    {
        return isVertical && size.Width != size.Height
            ? (size.Height, size.Width)
            : size;
    }

    private static bool TryGetGrid(TemplateItem template, string gridName, out Grid grid)
    {
        grid = template.Properties?.Grids?.FirstOrDefault(existing =>
                string.Equals(existing.Name, gridName, StringComparison.Ordinal)
                || string.Equals(existing.Id, gridName, StringComparison.Ordinal))
            ?? new Grid();
        return !string.IsNullOrWhiteSpace(grid.Name) || !string.IsNullOrWhiteSpace(grid.Id);
    }

    private static (int Width, int Height) GetItemSize(IReadOnlyDictionary<string, TemplateItem> templates, Item item)
    {
        if (!templates.TryGetValue(item.Template.ToString(), out var template))
        {
            return (1, 1);
        }

        var width = Math.Max(template.Properties?.Width ?? 1, 1);
        var height = Math.Max(template.Properties?.Height ?? 1, 1);
        return (width, height);
    }

    private static int? TryReadIndexedLocation(object? location)
    {
        return location switch
        {
            null => null,
            int value => value,
            long value => checked((int)value),
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var value) => value,
            _ => null,
        };
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

        var rotation = TryGetPropertyIgnoreCase(element, "r", out var rotationElement)
            ? rotationElement.GetString()
            : TryGetPropertyIgnoreCase(element, "rotation", out rotationElement)
                ? rotationElement.GetString()
                : null;
        placement = new GridPlacement(x, y, string.Equals(rotation, "Vertical", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
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

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static object CreateGridLocation(GridPlacement placement)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            x = placement.X,
            y = placement.Y,
            r = placement.IsVertical ? "Vertical" : "Horizontal",
        }));
        return document.RootElement.Clone();
    }

    private sealed record GridLocationEntry(
        Item Item,
        int Order,
        int? Index,
        GridPlacement? StructuredPlacement,
        (int Width, int Height) Size);

    private readonly record struct GridPlacement(int X, int Y, bool IsVertical);
    private readonly record struct PlacedRect(string ItemId, GridPlacement Placement, (int Width, int Height) Size);
}

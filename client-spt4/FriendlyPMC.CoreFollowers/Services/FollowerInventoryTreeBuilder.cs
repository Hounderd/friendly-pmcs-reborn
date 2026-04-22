using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed record FollowerInventoryOwnerSlotNode(
    string SlotId,
    FollowerInventoryItemViewDto? Item);

public sealed record FollowerInventoryTreeNode(
    FollowerInventoryItemViewDto Item,
    IReadOnlyList<FollowerInventoryTreeNode> Children);

public sealed record FollowerInventoryOwnerContainerNode(
    string SlotId,
    FollowerInventoryItemViewDto ContainerItem,
    IReadOnlyList<FollowerInventoryTreeNode> Items);

public sealed record FollowerInventoryOwnerTree(
    IReadOnlyList<FollowerInventoryOwnerSlotNode> EquipmentSlots,
    IReadOnlyList<FollowerInventoryOwnerContainerNode> ContainerGroups,
    IReadOnlyList<FollowerInventoryTreeNode> OverflowItems);

public static class FollowerInventoryTreeBuilder
{
    private static readonly string[] EquipmentSlotOrder =
    [
        "Headwear",
        "FaceCover",
        "Earpiece",
        "ArmorVest",
        "TacticalVest",
        "Backpack",
        "Pockets",
        "SecuredContainer",
        "Holster",
        "Scabbard",
        "FirstPrimaryWeapon",
        "SecondPrimaryWeapon",
    ];

    private static readonly string[] VisibleContainerSlotOrder =
    [
        "TacticalVest",
        "Backpack",
        "SecuredContainer",
        "Pockets",
    ];

    public static FollowerInventoryOwnerTree BuildFollowerTree(FollowerInventoryOwnerViewDto owner)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        var itemsById = owner.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var childrenByParentId = owner.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var rootItems = owner.Items
            .Where(item => string.Equals(item.ParentId, owner.RootId, StringComparison.Ordinal))
            .ToArray();
        var rootItemsBySlot = rootItems
            .Where(item => !string.IsNullOrWhiteSpace(item.SlotId))
            .GroupBy(item => item.SlotId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var reachableIds = new HashSet<string>(StringComparer.Ordinal);

        var equipmentSlots = EquipmentSlotOrder
            .Select(slotId =>
            {
                rootItemsBySlot.TryGetValue(slotId, out var item);
                if (item is not null)
                {
                    reachableIds.Add(item.Id);
                }

                return new FollowerInventoryOwnerSlotNode(slotId, item);
            })
            .ToArray();

        var containerGroups = new List<FollowerInventoryOwnerContainerNode>();
        foreach (var slotId in VisibleContainerSlotOrder)
        {
            if (!rootItemsBySlot.TryGetValue(slotId, out var containerItem))
            {
                continue;
            }

            var children = BuildChildren(containerItem.Id, childrenByParentId, reachableIds, new HashSet<string>(StringComparer.Ordinal)
            {
                containerItem.Id,
            });
            containerGroups.Add(new FollowerInventoryOwnerContainerNode(slotId, containerItem, children));
        }

        var overflowItems = owner.Items
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.ParentId)
                && !string.Equals(item.ParentId, owner.RootId, StringComparison.Ordinal)
                && !reachableIds.Contains(item.Id)
                && !HasReachableAncestor(item, itemsById, owner.RootId, reachableIds))
            .Select(item => new FollowerInventoryTreeNode(item, Array.Empty<FollowerInventoryTreeNode>()))
            .ToArray();

        return new FollowerInventoryOwnerTree(equipmentSlots, containerGroups, overflowItems);
    }

    private static IReadOnlyList<FollowerInventoryTreeNode> BuildChildren(
        string parentId,
        IReadOnlyDictionary<string, FollowerInventoryItemViewDto[]> childrenByParentId,
        HashSet<string> reachableIds,
        HashSet<string> ancestry)
    {
        if (!childrenByParentId.TryGetValue(parentId, out var children))
        {
            return Array.Empty<FollowerInventoryTreeNode>();
        }

        return children
            .OrderBy(child => child.SlotId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(child => child.Id, StringComparer.Ordinal)
            .Select(child =>
            {
                reachableIds.Add(child.Id);
                if (!ancestry.Add(child.Id))
                {
                    return new FollowerInventoryTreeNode(child, Array.Empty<FollowerInventoryTreeNode>());
                }

                var descendants = BuildChildren(child.Id, childrenByParentId, reachableIds, ancestry);
                ancestry.Remove(child.Id);
                return new FollowerInventoryTreeNode(child, descendants);
            })
            .ToArray();
    }

    private static bool HasReachableAncestor(
        FollowerInventoryItemViewDto item,
        IReadOnlyDictionary<string, FollowerInventoryItemViewDto> itemsById,
        string rootId,
        ICollection<string> reachableIds)
    {
        var currentParentId = item.ParentId;
        var safety = 0;
        while (!string.IsNullOrWhiteSpace(currentParentId) && safety++ < 64)
        {
            if (reachableIds.Contains(currentParentId))
            {
                return true;
            }

            if (string.Equals(currentParentId, rootId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!itemsById.TryGetValue(currentParentId, out var parent))
            {
                return false;
            }

            currentParentId = parent.ParentId;
        }

        return false;
    }
}

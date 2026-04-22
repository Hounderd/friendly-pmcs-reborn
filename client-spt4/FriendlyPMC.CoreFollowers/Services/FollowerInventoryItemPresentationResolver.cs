using FriendlyPMC.CoreFollowers.Models;

#if SPT_CLIENT
using EFT;
#endif

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerInventoryItemPresentationResolver
{
    public static bool IsEquipped(FollowerInventoryOwnerViewDto owner, FollowerInventoryItemViewDto item)
    {
        return string.Equals(item.ParentId, owner.RootId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(item.SlotId)
            && !string.Equals(item.SlotId, "hideout", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.SlotId, "main", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolvePrimaryText(FollowerInventoryItemViewDto item)
    {
#if SPT_CLIENT
        if (!string.IsNullOrWhiteSpace(item.TemplateId))
        {
            try
            {
                var localizedName = GClass2348.LocalizedName(new MongoID(item.TemplateId));
                if (!string.IsNullOrWhiteSpace(localizedName))
                {
                    return localizedName;
                }
            }
            catch
            {
            }
        }
#endif

        return item.TemplateId;
    }

    public static string ResolveSecondaryText(FollowerInventoryOwnerViewDto owner, FollowerInventoryItemViewDto item)
    {
        if (string.Equals(item.ParentId, owner.RootId, StringComparison.Ordinal))
        {
            var slotLabel = FollowerInventorySlotLabelFormatter.Format(item.SlotId);
            return string.Equals(item.SlotId, "hideout", StringComparison.OrdinalIgnoreCase)
                ? $"Stored in {slotLabel}"
                : $"Equipped: {slotLabel}";
        }

        var rootSlot = ResolveRootSlot(owner, item);
        return rootSlot is null
            ? FollowerInventorySlotLabelFormatter.Format(item.SlotId)
            : $"Stored in {FollowerInventorySlotLabelFormatter.Format(rootSlot)}";
    }

    private static string? ResolveRootSlot(FollowerInventoryOwnerViewDto owner, FollowerInventoryItemViewDto item)
    {
        var itemsById = owner.Items
            .GroupBy(existing => existing.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var currentParentId = item.ParentId;
        while (!string.IsNullOrWhiteSpace(currentParentId) && itemsById.TryGetValue(currentParentId, out var parent))
        {
            if (string.Equals(parent.ParentId, owner.RootId, StringComparison.Ordinal))
            {
                return parent.SlotId;
            }

            currentParentId = parent.ParentId;
        }

        return item.SlotId;
    }
}

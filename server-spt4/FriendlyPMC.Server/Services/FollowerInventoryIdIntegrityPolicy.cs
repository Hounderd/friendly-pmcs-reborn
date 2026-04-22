using FriendlyPMC.Server.Models;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json;

namespace FriendlyPMC.Server.Services;

internal static class FollowerInventoryIdIntegrityPolicy
{
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
}

using FriendlyPMC.Server.Models;

namespace FriendlyPMC.Server.Services;

public static class FollowerInventoryMigrationPolicy
{
    public static FollowerInventorySnapshot? CreateInventorySnapshot(FollowerEquipmentSnapshot? equipment)
    {
        if (equipment is null || string.IsNullOrWhiteSpace(equipment.EquipmentId))
        {
            return null;
        }

        return new FollowerInventorySnapshot(
            equipment.EquipmentId,
            equipment.Items
                .Select(item => new FollowerInventoryItemSnapshot(
                    item.Id,
                    item.TemplateId,
                    item.ParentId,
                    item.SlotId,
                    item.LocationJson,
                    item.UpdJson))
                .ToArray());
    }

    public static FollowerProfileSnapshot Upgrade(FollowerProfileSnapshot profile)
    {
        var inventory = profile.Inventory ?? CreateInventorySnapshot(profile.Equipment);
        var equipment = profile.Equipment ?? inventory?.ToEquipmentSnapshot();
        if (ReferenceEquals(inventory, profile.Inventory) && ReferenceEquals(equipment, profile.Equipment))
        {
            return profile;
        }

        return profile with
        {
            Equipment = equipment,
            Inventory = inventory,
        };
    }
}

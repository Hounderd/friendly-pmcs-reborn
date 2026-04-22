namespace FriendlyPMC.Server.Models;

public sealed record FollowerInventoryItemSnapshot(
    string Id,
    string TemplateId,
    string? ParentId,
    string? SlotId,
    string? LocationJson,
    string? UpdJson);

public sealed record FollowerInventorySnapshot(
    string EquipmentId,
    IReadOnlyList<FollowerInventoryItemSnapshot> Items)
{
    public FollowerEquipmentSnapshot ToEquipmentSnapshot()
    {
        return new FollowerEquipmentSnapshot(
            EquipmentId,
            Items.Select(item => new FollowerEquipmentItemSnapshot(
                    item.Id,
                    item.TemplateId,
                    item.ParentId,
                    item.SlotId,
                    item.LocationJson,
                    item.UpdJson))
                .ToArray());
    }
}

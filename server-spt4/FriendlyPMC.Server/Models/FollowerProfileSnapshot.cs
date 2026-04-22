namespace FriendlyPMC.Server.Models;

public sealed record HealthPartSnapshot(int Current, int Maximum)
{
    public HealthPartSnapshot Reset() => this with { Current = Maximum };
}

public sealed record FollowerEquipmentItemSnapshot(
    string Id,
    string TemplateId,
    string? ParentId,
    string? SlotId,
    string? LocationJson,
    string? UpdJson);

public sealed record FollowerEquipmentSnapshot(
    string EquipmentId,
    IReadOnlyList<FollowerEquipmentItemSnapshot> Items);

public sealed record FollowerAppearanceSnapshot(
    string? Head,
    string? Body,
    string? Feet,
    string? Hands,
    string? Voice,
    string? DogTag);

public sealed record FollowerHealthSnapshot(IReadOnlyDictionary<string, HealthPartSnapshot> Parts)
{
    public FollowerHealthSnapshot WithAllPartsHealed() => new(
        Parts.ToDictionary(
            part => part.Key,
            part => part.Value.Reset(),
            StringComparer.Ordinal));
}

public sealed record FollowerProfileSnapshot(
    string Aid,
    string Nickname,
    string Side,
    int Level,
    int Experience,
    IReadOnlyDictionary<string, int> SkillProgress,
    IReadOnlyList<string> InventoryItemIds,
    FollowerHealthSnapshot Health,
    FollowerEquipmentSnapshot? Equipment,
    FollowerAppearanceSnapshot? Appearance = null,
    FollowerInventorySnapshot? Inventory = null
);

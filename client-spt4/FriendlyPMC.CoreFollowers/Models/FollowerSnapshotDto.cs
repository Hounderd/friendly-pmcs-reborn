namespace FriendlyPMC.CoreFollowers.Models;

public sealed record FollowerEquipmentItemSnapshotDto(
    string Id,
    string TemplateId,
    string? ParentId,
    string? SlotId,
    string? LocationJson,
    string? UpdJson);

public sealed record FollowerEquipmentSnapshotDto(
    string EquipmentId,
    IReadOnlyList<FollowerEquipmentItemSnapshotDto> Items);

public sealed record FollowerAppearanceSnapshotDto(
    string? Head,
    string? Body,
    string? Feet,
    string? Hands,
    string? Voice,
    string? DogTag);

public sealed record FollowerSnapshotDto
{
    public FollowerSnapshotDto(
        string aid,
        string nickname,
        string side,
        int experience,
        IReadOnlyDictionary<string, int> skillProgress,
        IReadOnlyList<string> inventoryItemIds,
        IReadOnlyDictionary<string, int> healthValues)
        : this(
            aid,
            nickname,
            side,
            0,
            experience,
            skillProgress,
            inventoryItemIds,
            healthValues,
            healthValues,
            null,
            null)
    {
    }

    public FollowerSnapshotDto(
        string aid,
        string nickname,
        string side,
        int experience,
        IReadOnlyDictionary<string, int> skillProgress,
        IReadOnlyList<string> inventoryItemIds,
        IReadOnlyDictionary<string, int> healthValues,
        IReadOnlyDictionary<string, int> healthMaximumValues)
        : this(
            aid,
            nickname,
            side,
            0,
            experience,
            skillProgress,
            inventoryItemIds,
            healthValues,
            healthMaximumValues,
            null,
            null)
    {
    }

    public FollowerSnapshotDto(
        string aid,
        string nickname,
        string side,
        int level,
        int experience,
        IReadOnlyDictionary<string, int> skillProgress,
        IReadOnlyList<string> inventoryItemIds,
        IReadOnlyDictionary<string, int> healthValues,
        IReadOnlyDictionary<string, int> healthMaximumValues,
        FollowerEquipmentSnapshotDto? equipment,
        FollowerAppearanceSnapshotDto? appearance = null)
    {
        Aid = aid;
        Nickname = nickname;
        Side = side;
        Level = level;
        Experience = experience;
        SkillProgress = skillProgress;
        InventoryItemIds = inventoryItemIds;
        HealthValues = healthValues;
        HealthMaximumValues = healthMaximumValues;
        Equipment = equipment;
        Appearance = appearance;
    }

    public string Aid { get; init; }

    public string Nickname { get; init; }

    public string Side { get; init; }

    public int Level { get; init; }

    public int Experience { get; init; }

    public IReadOnlyDictionary<string, int> SkillProgress { get; init; }

    public IReadOnlyList<string> InventoryItemIds { get; init; }

    public IReadOnlyDictionary<string, int> HealthValues { get; init; }

    public IReadOnlyDictionary<string, int> HealthMaximumValues { get; init; }

    public FollowerEquipmentSnapshotDto? Equipment { get; init; }

    public FollowerAppearanceSnapshotDto? Appearance { get; init; }
}

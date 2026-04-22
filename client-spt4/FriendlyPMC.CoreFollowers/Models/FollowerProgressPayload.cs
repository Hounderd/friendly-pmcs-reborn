namespace FriendlyPMC.CoreFollowers.Models;

public sealed record FollowerProgressPayload(
    string Aid,
    int Experience,
    IReadOnlyDictionary<string, int> SkillProgress,
    IReadOnlyList<string> InventoryItemIds
);

using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerSnapshotMapper
{
    public static FollowerProgressPayload ToProgressPayload(FollowerSnapshotDto snapshot)
    {
        return new FollowerProgressPayload(
            snapshot.Aid,
            snapshot.Experience,
            new Dictionary<string, int>(snapshot.SkillProgress),
            snapshot.InventoryItemIds.ToArray());
    }
}

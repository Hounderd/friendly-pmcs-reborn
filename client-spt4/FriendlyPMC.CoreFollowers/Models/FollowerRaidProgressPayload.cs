namespace FriendlyPMC.CoreFollowers.Models;

public sealed record FollowerRaidProgressPayload(
    IReadOnlyList<FollowerSnapshotDto> Followers,
    IReadOnlyList<string> RaidStartFollowerAids,
    IReadOnlyList<string> SpawnedFollowerAids,
    IReadOnlyList<string> DeadFollowerAids);

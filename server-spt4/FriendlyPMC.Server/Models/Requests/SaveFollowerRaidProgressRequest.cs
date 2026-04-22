using FriendlyPMC.Server.Models;
using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record SaveFollowerRaidProgressRequest(
    IReadOnlyList<FollowerProfileSnapshot> Followers,
    IReadOnlyList<string>? RaidStartFollowerAids,
    IReadOnlyList<string>? SpawnedFollowerAids,
    IReadOnlyList<string>? DeadFollowerAids) : IRequestData;

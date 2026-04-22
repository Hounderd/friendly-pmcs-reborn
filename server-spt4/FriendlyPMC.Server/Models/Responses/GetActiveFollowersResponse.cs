using FriendlyPMC.Server.Models;

namespace FriendlyPMC.Server.Models.Responses;

public sealed record GetActiveFollowersResponse(IReadOnlyList<FollowerProfileSnapshot> Followers);

using FriendlyPMC.Server.Models;

namespace FriendlyPMC.Server.Models.Responses;

public sealed record FollowerInventoryOwnerViewResponse(
    string Owner,
    string RootId,
    IReadOnlyList<FollowerInventoryItemSnapshot> Items);

public sealed record GetFollowerInventoryResponse(
    string FollowerAid,
    string Nickname,
    FollowerInventoryOwnerViewResponse? Player,
    FollowerInventoryOwnerViewResponse? Follower,
    string? ErrorMessage = null,
    string? DebugDetails = null);

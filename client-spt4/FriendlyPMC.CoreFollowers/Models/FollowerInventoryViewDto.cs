namespace FriendlyPMC.CoreFollowers.Models;

public sealed record FollowerInventoryItemViewDto(
    string Id,
    string TemplateId,
    string? ParentId,
    string? SlotId,
    string? LocationJson,
    string? UpdJson);

public sealed record FollowerInventoryOwnerViewDto(
    string Owner,
    string RootId,
    IReadOnlyList<FollowerInventoryItemViewDto> Items);

public sealed record FollowerInventoryViewDto(
    string FollowerAid,
    string Nickname,
    FollowerInventoryOwnerViewDto? Player,
    FollowerInventoryOwnerViewDto? Follower,
    string? ErrorMessage = null,
    string? DebugDetails = null);

public sealed record FollowerInventoryMovePayload(
    string FollowerAid,
    string SourceOwner,
    string ItemId,
    string ToId,
    string ToContainer,
    string? ToLocationJson);

public sealed record FollowerInventoryMoveResultDto(
    bool Succeeded,
    string? ErrorMessage);

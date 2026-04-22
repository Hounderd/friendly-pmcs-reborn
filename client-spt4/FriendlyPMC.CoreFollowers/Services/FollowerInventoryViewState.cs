using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed record FollowerInventoryViewState(
    string FollowerAid,
    string Nickname,
    FollowerInventoryMode Mode,
    bool IsLoading,
    string? ErrorMessage,
    string? DebugDetails,
    FollowerInventoryOwnerViewDto? Player,
    FollowerInventoryOwnerViewDto? Follower,
    bool CanTransferFromPlayerToFollower,
    bool CanTransferFromFollowerToPlayer,
    bool ShowTakeAllAction,
    bool ShowContinueAction);

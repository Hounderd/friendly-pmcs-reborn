using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record FollowerInventoryMoveRequest(
    string FollowerAid,
    string SourceOwner,
    string ItemId,
    string ToId,
    string ToContainer,
    string? ToLocationJson) : IRequestData;

using FriendlyPMC.Server.Models;

namespace FriendlyPMC.Server.Models.Responses;

public sealed record FollowerInventoryMoveResponse(
    bool Succeeded,
    string? ErrorMessage,
    FollowerInventorySnapshot? Inventory);

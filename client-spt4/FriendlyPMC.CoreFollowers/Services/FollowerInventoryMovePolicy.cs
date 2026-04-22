using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerInventoryMovePolicy
{
    public static FollowerInventoryMovePayload CreatePayload(
        FollowerInventoryViewState state,
        string sourceOwner,
        string itemId,
        string toId,
        string toContainer,
        string? toLocationJson)
    {
        if (state.Player is null || state.Follower is null)
        {
            throw new InvalidOperationException("Follower inventory is not loaded.");
        }

        var normalizedSourceOwner = sourceOwner?.Trim() ?? string.Empty;
        if (string.Equals(normalizedSourceOwner, "player", StringComparison.OrdinalIgnoreCase))
        {
            if (!state.CanTransferFromPlayerToFollower)
            {
                throw new InvalidOperationException("Player to follower inventory moves are not allowed in the current mode.");
            }
        }
        else if (string.Equals(normalizedSourceOwner, "follower", StringComparison.OrdinalIgnoreCase))
        {
            if (!state.CanTransferFromFollowerToPlayer)
            {
                throw new InvalidOperationException("Follower to player inventory moves are not allowed in the current mode.");
            }
        }
        else
        {
            throw new InvalidOperationException("Inventory move source owner was not recognized.");
        }

        return new FollowerInventoryMovePayload(
            state.FollowerAid,
            normalizedSourceOwner.ToLowerInvariant(),
            itemId,
            toId,
            toContainer,
            toLocationJson);
    }
}

using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed class FollowerInventoryPresenter
{
    private readonly IFollowerApiClient apiClient;

    public FollowerInventoryPresenter(IFollowerApiClient apiClient)
    {
        this.apiClient = apiClient;
        CurrentState = CreateLoadingState(string.Empty, string.Empty, FollowerInventoryMode.Management);
    }

    public FollowerInventoryViewState CurrentState { get; private set; }

    public async Task<FollowerInventoryViewState> LoadAsync(string followerAid, string nickname, FollowerInventoryMode mode)
    {
        CurrentState = CreateLoadingState(followerAid, nickname, mode);

        try
        {
            var inventory = await apiClient.GetFollowerInventoryAsync(followerAid);
            if (inventory is null)
            {
                CurrentState = CreateErrorState(followerAid, nickname, mode, "Follower inventory was not available.", null);
                return CurrentState;
            }

            if (!string.IsNullOrWhiteSpace(inventory.ErrorMessage))
            {
                CurrentState = CreateErrorState(
                    followerAid,
                    nickname,
                    mode,
                    inventory.ErrorMessage,
                    inventory.DebugDetails);
                return CurrentState;
            }

            CurrentState = CreateLoadedState(
                inventory.FollowerAid,
                string.IsNullOrWhiteSpace(inventory.Nickname) ? nickname : inventory.Nickname,
                mode,
                inventory.Player ?? new FollowerInventoryOwnerViewDto("player", string.Empty, Array.Empty<FollowerInventoryItemViewDto>()),
                inventory.Follower ?? new FollowerInventoryOwnerViewDto("follower", string.Empty, Array.Empty<FollowerInventoryItemViewDto>()));
            return CurrentState;
        }
        catch (Exception ex)
        {
            CurrentState = CreateErrorState(followerAid, nickname, mode, ex.Message, ex.ToString());
            return CurrentState;
        }
    }

    public async Task<FollowerInventoryMoveResultDto> MoveAsync(
        string sourceOwner,
        string itemId,
        string toId,
        string toContainer,
        string? toLocationJson)
    {
        var payload = FollowerInventoryMovePolicy.CreatePayload(
            CurrentState,
            sourceOwner,
            itemId,
            toId,
            toContainer,
            toLocationJson);
        var moveResult = await apiClient.MoveFollowerInventoryItemAsync(payload);
        if (!moveResult.Succeeded)
        {
            CurrentState = CurrentState with { ErrorMessage = moveResult.ErrorMessage };
            return moveResult;
        }

        await LoadAsync(CurrentState.FollowerAid, CurrentState.Nickname, CurrentState.Mode);
        return moveResult;
    }

    private static FollowerInventoryViewState CreateLoadingState(string followerAid, string nickname, FollowerInventoryMode mode)
    {
        var flags = ResolveModeFlags(mode);
        return new FollowerInventoryViewState(
            followerAid,
            nickname,
            mode,
            true,
            null,
            null,
            null,
            null,
            flags.CanTransferFromPlayerToFollower,
            flags.CanTransferFromFollowerToPlayer,
            flags.ShowTakeAllAction,
            flags.ShowContinueAction);
    }

    private static FollowerInventoryViewState CreateLoadedState(
        string followerAid,
        string nickname,
        FollowerInventoryMode mode,
        FollowerInventoryOwnerViewDto player,
        FollowerInventoryOwnerViewDto follower)
    {
        var flags = ResolveModeFlags(mode);
        return new FollowerInventoryViewState(
            followerAid,
            nickname,
            mode,
            false,
            null,
            null,
            player,
            follower,
            flags.CanTransferFromPlayerToFollower,
            flags.CanTransferFromFollowerToPlayer,
            flags.ShowTakeAllAction,
            flags.ShowContinueAction);
    }

    private static FollowerInventoryViewState CreateErrorState(
        string followerAid,
        string nickname,
        FollowerInventoryMode mode,
        string errorMessage,
        string? debugDetails)
    {
        var flags = ResolveModeFlags(mode);
        return new FollowerInventoryViewState(
            followerAid,
            nickname,
            mode,
            false,
            errorMessage,
            debugDetails,
            null,
            null,
            flags.CanTransferFromPlayerToFollower,
            flags.CanTransferFromFollowerToPlayer,
            flags.ShowTakeAllAction,
            flags.ShowContinueAction);
    }

    private static (bool CanTransferFromPlayerToFollower, bool CanTransferFromFollowerToPlayer, bool ShowTakeAllAction, bool ShowContinueAction) ResolveModeFlags(FollowerInventoryMode mode)
    {
        return mode switch
        {
            FollowerInventoryMode.PostRaidTransfer => (false, true, true, true),
            _ => (true, true, false, false),
        };
    }
}

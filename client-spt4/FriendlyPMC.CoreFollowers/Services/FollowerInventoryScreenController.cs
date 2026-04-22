using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed class FollowerInventoryScreenController
{
    private const string PlayerStashGridContainer = "hideout";
    private readonly FollowerInventoryPresenter presenter;
    private readonly IFollowerInventoryRuntimeViewFactory runtimeViewFactory;
    private readonly IFollowerInventoryTargetResolver targetResolver;
    private readonly IFollowerProfileScreenRefresher profileScreenRefresher;
    private readonly IFollowerPlayerInventoryRefresher playerInventoryRefresher;
    private readonly Action<string>? logInfo;
    private readonly Action<string, Exception>? logError;
    private IFollowerInventoryRuntimeView? activeRuntimeView;
    private string selectedOwner = string.Empty;
    private string selectedItemId = string.Empty;
    private string selectedTargetKey = string.Empty;

    public FollowerInventoryScreenController(
        FollowerInventoryPresenter presenter,
        IFollowerInventoryRuntimeViewFactory? runtimeViewFactory = null,
        IFollowerInventoryTargetResolver? targetResolver = null,
        IFollowerProfileScreenRefresher? profileScreenRefresher = null,
        IFollowerPlayerInventoryRefresher? playerInventoryRefresher = null,
        Action<string>? logInfo = null,
        Action<string, Exception>? logError = null)
    {
        this.presenter = presenter;
        this.runtimeViewFactory = runtimeViewFactory ?? new FollowerInventoryRuntimeViewFactory();
        this.targetResolver = targetResolver ?? new FollowerInventoryTargetResolver();
        this.profileScreenRefresher = profileScreenRefresher ?? new FollowerProfileScreenRefresher();
        this.playerInventoryRefresher = playerInventoryRefresher ?? new FollowerPlayerInventoryRefresher();
        this.logInfo = logInfo;
        this.logError = logError;
    }

    public FollowerInventoryPresenter Presenter => presenter;

    public async Task<FollowerInventoryViewState> OpenManagementAsync(string followerAid, string nickname, object? hostScreen = null)
    {
        try
        {
            Close();
            selectedOwner = string.Empty;
            selectedItemId = string.Empty;
            selectedTargetKey = string.Empty;
            ShowState(hostScreen, new FollowerInventoryViewState(
                followerAid,
                nickname,
                FollowerInventoryMode.Management,
                true,
                null,
                null,
                null,
                null,
                true,
                true,
                false,
                false));
            logInfo?.Invoke($"Follower inventory open requested: follower={nickname}, aid={followerAid}, mode={FollowerInventoryMode.Management}");
            var state = await presenter.LoadAsync(followerAid, nickname, FollowerInventoryMode.Management);
            ShowState(hostScreen, state);
            if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
            {
                var debugSuffix = string.IsNullOrWhiteSpace(state.DebugDetails)
                    ? string.Empty
                    : $", debug={state.DebugDetails}";
                logInfo?.Invoke($"Follower inventory load failed: follower={state.Nickname}, aid={state.FollowerAid}, error={state.ErrorMessage}{debugSuffix}");
                return state;
            }

            logInfo?.Invoke(
                $"Follower inventory loaded: follower={state.Nickname}, aid={state.FollowerAid}, playerItems={state.Player?.Items.Count ?? 0}, followerItems={state.Follower?.Items.Count ?? 0}");
            return state;
        }
        catch (Exception ex)
        {
            logError?.Invoke($"Failed to open follower inventory: follower={nickname}, aid={followerAid}", ex);
            throw;
        }
    }

    public void SelectItem(string owner, string itemId)
    {
        selectedOwner = owner ?? string.Empty;
        selectedItemId = itemId ?? string.Empty;
        var availableTargets = targetResolver.ResolveTargets(presenter.CurrentState, selectedOwner, selectedItemId);
        selectedTargetKey = availableTargets.FirstOrDefault()?.Key ?? string.Empty;
        logInfo?.Invoke(
            $"Follower inventory item selected: follower={presenter.CurrentState.Nickname}, aid={presenter.CurrentState.FollowerAid}, owner={selectedOwner}, item={selectedItemId}, targets={availableTargets.Count}");
        ShowState(null, presenter.CurrentState);
    }

    public void SelectTarget(string targetKey)
    {
        selectedTargetKey = targetKey ?? string.Empty;
        logInfo?.Invoke(
            $"Follower inventory target selected: follower={presenter.CurrentState.Nickname}, aid={presenter.CurrentState.FollowerAid}, target={selectedTargetKey}");
        ShowState(null, presenter.CurrentState);
    }

    public async Task<FollowerInventoryMoveResultDto> TransferSelectedAsync()
    {
        return await TransferDraggedAsync(selectedOwner, selectedItemId, selectedTargetKey);
    }

    public async Task<FollowerInventoryMoveResultDto> TransferDraggedAsync(string sourceOwner, string itemId, string? targetKey)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return new FollowerInventoryMoveResultDto(false, "No transferable item is selected.");
        }

        var normalizedSourceOwner = sourceOwner?.Trim() ?? string.Empty;
        var normalizedTargetKey = targetKey?.Trim() ?? string.Empty;
        logInfo?.Invoke(
            $"Follower inventory transfer requested: follower={presenter.CurrentState.Nickname}, aid={presenter.CurrentState.FollowerAid}, owner={normalizedSourceOwner}, item={itemId}, target={normalizedTargetKey}");

        FollowerInventoryMoveResultDto result;
        if (string.Equals(normalizedSourceOwner, "follower", StringComparison.OrdinalIgnoreCase)
            && presenter.CurrentState.Player is not null)
        {
            result = await presenter.MoveAsync(
                "follower",
                itemId,
                presenter.CurrentState.Player.RootId,
                PlayerStashGridContainer,
                null);
        }
        else if (string.Equals(normalizedSourceOwner, "player", StringComparison.OrdinalIgnoreCase))
        {
            var resolvedTargets = targetResolver.ResolveTargets(presenter.CurrentState, normalizedSourceOwner, itemId);
            var selectedTarget = string.IsNullOrWhiteSpace(normalizedTargetKey)
                ? resolvedTargets.FirstOrDefault()
                : resolvedTargets.FirstOrDefault(target => string.Equals(target.Key, normalizedTargetKey, StringComparison.Ordinal));
            if (selectedTarget is null)
            {
                var errorMessage = string.IsNullOrWhiteSpace(normalizedTargetKey)
                    ? "No follower target is selected."
                    : DescribeExplicitTargetError(normalizedTargetKey);
                logInfo?.Invoke(
                    $"Follower inventory transfer rejected before move: follower={presenter.CurrentState.Nickname}, aid={presenter.CurrentState.FollowerAid}, owner={normalizedSourceOwner}, item={itemId}, target={normalizedTargetKey}, error={errorMessage}");
                ShowState(null, presenter.CurrentState with { ErrorMessage = errorMessage, DebugDetails = null });
                return new FollowerInventoryMoveResultDto(false, errorMessage);
            }

            if (string.IsNullOrWhiteSpace(normalizedTargetKey))
            {
                logInfo?.Invoke(
                    $"Follower inventory transfer resolved generic drop target: follower={presenter.CurrentState.Nickname}, aid={presenter.CurrentState.FollowerAid}, owner={normalizedSourceOwner}, item={itemId}, target={selectedTarget.Key}");
            }

            result = await presenter.MoveAsync(
                "player",
                itemId,
                selectedTarget.ToId,
                selectedTarget.ToContainer,
                selectedTarget.ToLocationJson);
        }
        else
        {
            return new FollowerInventoryMoveResultDto(false, "No transferable item is selected.");
        }

        if (result.Succeeded)
        {
            selectedOwner = string.Empty;
            selectedItemId = string.Empty;
            selectedTargetKey = string.Empty;
            logInfo?.Invoke(
                $"Follower inventory transfer completed: follower={presenter.CurrentState.Nickname}, aid={presenter.CurrentState.FollowerAid}");
            ShowState(null, presenter.CurrentState);
            await TryRefreshLivePlayerInventoryAsync(presenter.CurrentState.Player);
            await TryRefreshVisibleProfileAsync(presenter.CurrentState.FollowerAid);
        }
        else
        {
            logInfo?.Invoke(
                $"Follower inventory transfer rejected: follower={presenter.CurrentState.Nickname}, aid={presenter.CurrentState.FollowerAid}, error={result.ErrorMessage}");
            ShowState(null, presenter.CurrentState);
        }
        return result;
    }

    private static string DescribeExplicitTargetError(string targetKey)
    {
        if (targetKey.StartsWith("equip:", StringComparison.Ordinal))
        {
            var slotId = targetKey["equip:".Length..];
            return $"Item cannot be equipped to {FollowerInventorySlotLabelFormatter.Format(slotId)}.";
        }

        if (targetKey.StartsWith("store:", StringComparison.Ordinal))
        {
            return "Item cannot be stored in that container.";
        }

        return "No follower target is selected.";
    }

    private async Task TryRefreshVisibleProfileAsync(string followerAid)
    {
        try
        {
            await profileScreenRefresher.RefreshAfterInventoryMoveAsync(followerAid);
        }
        catch (Exception ex)
        {
            logError?.Invoke($"Failed to refresh visible follower profile after inventory move: aid={followerAid}", ex);
        }
    }

    private async Task TryRefreshLivePlayerInventoryAsync(FollowerInventoryOwnerViewDto? playerInventory)
    {
        try
        {
            await playerInventoryRefresher.RefreshAfterInventoryMoveAsync(playerInventory);
        }
        catch (Exception ex)
        {
            logError?.Invoke("Failed to refresh live player inventory after follower move.", ex);
        }
    }

    public void Close()
    {
        activeRuntimeView?.Dispose();
        activeRuntimeView = null;
    }

    private void ShowState(object? hostScreen, FollowerInventoryViewState state)
    {
        activeRuntimeView ??= runtimeViewFactory.Create(hostScreen, new FollowerInventoryScreenActions(
            Close,
            SelectItem,
            SelectTarget,
            TransferSelectedAsync,
            TransferDraggedAsync,
            message => logInfo?.Invoke($"Follower inventory ui: {message}")));
        var availableTargets = targetResolver.ResolveTargets(state, selectedOwner, selectedItemId);
        if (availableTargets.Count > 0 && !availableTargets.Any(target => string.Equals(target.Key, selectedTargetKey, StringComparison.Ordinal)))
        {
            selectedTargetKey = availableTargets[0].Key;
        }

        activeRuntimeView.Render(FollowerInventoryScreenViewModelFactory.Create(state, selectedOwner, selectedItemId, availableTargets, selectedTargetKey));
    }
}

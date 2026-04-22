using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed record FollowerInventoryDebugProbeOptions(
    string FollowerAid,
    string Nickname,
    bool SelectFirstFollowerItem = false,
    bool SelectFirstPlayerItem = false,
    bool TransferSelectedItem = false);

public sealed record FollowerInventoryDebugProbeReport(
    FollowerInventoryViewState State,
    string? SelectedOwner,
    string? SelectedItemId,
    FollowerInventoryMoveResultDto? MoveResult,
    FollowerInventoryScreenViewModel? LastRenderedModel,
    IReadOnlyList<FollowerInventoryScreenViewModel> RenderedModels);

public sealed class FollowerInventoryDebugProbe
{
    private readonly IFollowerApiClient apiClient;

    public FollowerInventoryDebugProbe(IFollowerApiClient apiClient)
    {
        this.apiClient = apiClient;
    }

    public async Task<FollowerInventoryDebugProbeReport> RunAsync(FollowerInventoryDebugProbeOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var runtimeView = new CaptureRuntimeView();
        var controller = new FollowerInventoryScreenController(
            new FollowerInventoryPresenter(apiClient),
            new CaptureRuntimeViewFactory(runtimeView));

        try
        {
            var state = await controller.OpenManagementAsync(options.FollowerAid, options.Nickname, new object());
            var selectedOwner = string.Empty;
            var selectedItemId = string.Empty;

            if (options.SelectFirstFollowerItem || options.SelectFirstPlayerItem || options.TransferSelectedItem)
            {
                var firstSelection = options.SelectFirstPlayerItem
                    ? ("player", state.Player?.Items.FirstOrDefault(IsTransferableItem)?.Id)
                    : ("follower", state.Follower?.Items.FirstOrDefault(IsTransferableItem)?.Id);
                if (!string.IsNullOrWhiteSpace(firstSelection.Item2))
                {
                    selectedOwner = firstSelection.Item1;
                    selectedItemId = firstSelection.Item2!;
                    controller.SelectItem(selectedOwner, selectedItemId);
                }
            }

            FollowerInventoryMoveResultDto? moveResult = null;
            if (options.TransferSelectedItem)
            {
                moveResult = await controller.TransferSelectedAsync();
            }

            state = controller.Presenter.CurrentState;

            return new FollowerInventoryDebugProbeReport(
                state,
                string.IsNullOrWhiteSpace(selectedOwner) ? null : selectedOwner,
                string.IsNullOrWhiteSpace(selectedItemId) ? null : selectedItemId,
                moveResult,
                runtimeView.RenderedModels.LastOrDefault(),
                runtimeView.RenderedModels.ToArray());
        }
        finally
        {
            controller.Close();
        }
    }

    private sealed class CaptureRuntimeViewFactory : IFollowerInventoryRuntimeViewFactory
    {
        private readonly CaptureRuntimeView runtimeView;

        public CaptureRuntimeViewFactory(CaptureRuntimeView runtimeView)
        {
            this.runtimeView = runtimeView;
        }

        public IFollowerInventoryRuntimeView Create(object? hostScreen, FollowerInventoryScreenActions actions)
        {
            runtimeView.Actions = actions;
            runtimeView.CreatedWithHost = hostScreen;
            return runtimeView;
        }
    }

    private sealed class CaptureRuntimeView : IFollowerInventoryRuntimeView
    {
        public List<FollowerInventoryScreenViewModel> RenderedModels { get; } = new();

        public object? CreatedWithHost { get; set; }

        public FollowerInventoryScreenActions? Actions { get; set; }

        public void Render(FollowerInventoryScreenViewModel model)
        {
            RenderedModels.Add(model);
        }

        public void Dispose()
        {
        }
    }

    private static bool IsTransferableItem(FollowerInventoryItemViewDto item)
    {
        return !string.IsNullOrWhiteSpace(item.ParentId);
    }
}

namespace FriendlyPMC.CoreFollowers.Services;

public sealed record FollowerInventoryScreenItemViewModel(
    string Id,
    string Owner,
    string TemplateId,
    string PrimaryText,
    string SecondaryText,
    bool IsSelected,
    int Depth = 0,
    IReadOnlyList<FollowerInventoryScreenItemViewModel>? Children = null);

public sealed record FollowerInventoryFollowerSlotViewModel(
    string SlotId,
    string Label,
    FollowerInventoryScreenItemViewModel? Item);

public sealed record FollowerInventoryFollowerContainerViewModel(
    string SlotId,
    string Label,
    string ContainerItemId,
    IReadOnlyList<FollowerInventoryScreenItemViewModel> Items);

public sealed record FollowerInventoryFollowerPaneViewModel(
    IReadOnlyList<FollowerInventoryFollowerSlotViewModel> EquipmentSlots,
    IReadOnlyList<FollowerInventoryFollowerContainerViewModel> Containers,
    IReadOnlyList<FollowerInventoryScreenItemViewModel> OverflowItems);

public sealed record FollowerInventoryScreenTargetViewModel(
    string Key,
    string Label,
    bool IsSelected,
    bool IsEquipTarget);

public sealed record FollowerInventoryScreenSectionViewModel(
    string Title,
    string Summary,
    IReadOnlyList<FollowerInventoryScreenItemViewModel> Items);

public sealed record FollowerInventoryScreenViewModel(
    string Title,
    string StatusText,
    string? ErrorMessage,
    string? DebugDetails,
    string? PrimaryActionText,
    bool CanRunPrimaryAction,
    IReadOnlyList<FollowerInventoryScreenTargetViewModel> AvailableTargets,
    IReadOnlyList<FollowerInventoryScreenSectionViewModel> Sections,
    FollowerInventoryFollowerPaneViewModel? FollowerPane = null);

public sealed record FollowerInventoryScreenActions(
    Action Close,
    Action<string, string> SelectItem,
    Action<string> SelectTarget,
    Func<Task> RunPrimaryActionAsync);

public interface IFollowerInventoryRuntimeView : IDisposable
{
    void Render(FollowerInventoryScreenViewModel model);
}

public interface IFollowerInventoryRuntimeViewFactory
{
    IFollowerInventoryRuntimeView Create(object? hostScreen, FollowerInventoryScreenActions actions);
}

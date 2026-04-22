using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerInventoryScreenViewModelFactory
{
    public static FollowerInventoryScreenViewModel Create(
        FollowerInventoryViewState state,
        string? selectedOwner = null,
        string? selectedItemId = null,
        IReadOnlyList<FollowerInventoryTargetViewModel>? availableTargets = null,
        string? selectedTargetKey = null)
    {
        var sections = new List<FollowerInventoryScreenSectionViewModel>();
        if (state.Follower is not null)
        {
            sections.Add(CreateSection("Follower", "follower", state.Follower, selectedOwner, selectedItemId));
        }

        if (state.Player is not null)
        {
            sections.Add(CreateSection("Player", "player", state.Player, selectedOwner, selectedItemId));
        }

        availableTargets ??= Array.Empty<FollowerInventoryTargetViewModel>();
        var targetViewModels = availableTargets
            .Select(target => new FollowerInventoryScreenTargetViewModel(
                target.Key,
                CompactTargetLabel(target),
                string.Equals(target.Key, selectedTargetKey, StringComparison.Ordinal),
                target.IsEquipTarget))
            .ToArray();
        var primaryActionText = ResolvePrimaryActionText(state, selectedOwner, selectedItemId, availableTargets, selectedTargetKey);
        var followerPane = state.Follower is null
            ? null
            : CreateFollowerPane(state.Follower, selectedOwner, selectedItemId);
        return new FollowerInventoryScreenViewModel(
            $"{state.Nickname} Inventory",
            ResolveStatusText(state),
            state.ErrorMessage,
            state.DebugDetails,
            primaryActionText,
            !string.IsNullOrWhiteSpace(primaryActionText),
            targetViewModels,
            sections,
            followerPane);
    }

    private static string ResolveStatusText(FollowerInventoryViewState state)
    {
        if (state.IsLoading)
        {
            return "Loading inventory...";
        }

        return state.Mode switch
        {
            FollowerInventoryMode.PostRaidTransfer => "Post-Raid Transfer",
            _ => "Management",
        };
    }

    private static FollowerInventoryScreenSectionViewModel CreateSection(
        string title,
        string ownerKey,
        FollowerInventoryOwnerViewDto owner,
        string? selectedOwner,
        string? selectedItemId)
    {
        var items = owner.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .OrderBy(item => FollowerInventoryItemPresentationResolver.IsEquipped(owner, item) ? 0 : 1)
            .ThenBy(item => item.SlotId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new FollowerInventoryScreenItemViewModel(
                item.Id,
                ownerKey,
                item.TemplateId,
                FollowerInventoryItemPresentationResolver.ResolvePrimaryText(item),
                FollowerInventoryItemPresentationResolver.ResolveSecondaryText(owner, item),
                string.Equals(ownerKey, selectedOwner, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Id, selectedItemId, StringComparison.Ordinal)))
            .ToArray();

        return new FollowerInventoryScreenSectionViewModel(
            title,
            $"{items.Length} {(items.Length == 1 ? "item" : "items")}",
            items);
    }

    private static string? ResolvePrimaryActionText(
        FollowerInventoryViewState state,
        string? selectedOwner,
        string? selectedItemId,
        IReadOnlyList<FollowerInventoryTargetViewModel> availableTargets,
        string? selectedTargetKey)
    {
        if (string.IsNullOrWhiteSpace(selectedItemId))
        {
            return null;
        }

        if (string.Equals(selectedOwner, "follower", StringComparison.OrdinalIgnoreCase)
            && state.CanTransferFromFollowerToPlayer)
        {
            return "TAKE TO STASH";
        }

        if (string.Equals(selectedOwner, "player", StringComparison.OrdinalIgnoreCase)
            && state.CanTransferFromPlayerToFollower)
        {
            var selectedTarget = availableTargets.FirstOrDefault(target => string.Equals(target.Key, selectedTargetKey, StringComparison.Ordinal));
            if (selectedTarget is null)
            {
                return null;
            }

            var targetLabel = CompactTargetLabel(selectedTarget);
            return selectedTarget.IsEquipTarget
                ? $"EQUIP TO {targetLabel.ToUpperInvariant()}"
                : $"STORE IN {targetLabel.ToUpperInvariant()}";
        }

        return null;
    }

    private static string CompactTargetLabel(FollowerInventoryTargetViewModel target)
    {
        if (target.IsEquipTarget)
        {
            return target.Label.Replace("Equip: ", string.Empty, StringComparison.Ordinal);
        }

        return target.Label.Replace("Store: ", string.Empty, StringComparison.Ordinal);
    }

    private static FollowerInventoryFollowerPaneViewModel CreateFollowerPane(
        FollowerInventoryOwnerViewDto owner,
        string? selectedOwner,
        string? selectedItemId)
    {
        var tree = FollowerInventoryTreeBuilder.BuildFollowerTree(owner);
        var equipmentSlots = tree.EquipmentSlots
            .Select(slot => new FollowerInventoryFollowerSlotViewModel(
                slot.SlotId,
                FollowerInventorySlotLabelFormatter.Format(slot.SlotId),
                slot.Item is null ? null : CreateItemViewModel(owner, "follower", slot.Item, selectedOwner, selectedItemId)))
            .ToArray();
        var containers = tree.ContainerGroups
            .Select(container => new FollowerInventoryFollowerContainerViewModel(
                container.SlotId,
                FollowerInventorySlotLabelFormatter.Format(container.SlotId),
                container.ContainerItem.Id,
                FollowerInventoryTreeTraversal.Flatten(container.Items)
                    .Select(entry => CreateTreeItemViewModel(owner, "follower", entry.Node, entry.Depth, selectedOwner, selectedItemId))
                    .ToArray()))
            .ToArray();
        var overflowItems = FollowerInventoryTreeTraversal.Flatten(tree.OverflowItems)
            .Select(entry => CreateTreeItemViewModel(owner, "follower", entry.Node, entry.Depth, selectedOwner, selectedItemId))
            .ToArray();

        return new FollowerInventoryFollowerPaneViewModel(equipmentSlots, containers, overflowItems);
    }

    private static FollowerInventoryScreenItemViewModel CreateTreeItemViewModel(
        FollowerInventoryOwnerViewDto owner,
        string ownerKey,
        FollowerInventoryTreeNode node,
        int depth,
        string? selectedOwner,
        string? selectedItemId)
    {
        var item = CreateItemViewModel(owner, ownerKey, node.Item, selectedOwner, selectedItemId);
        return item with
        {
            Depth = depth,
        };
    }

    private static FollowerInventoryScreenItemViewModel CreateItemViewModel(
        FollowerInventoryOwnerViewDto owner,
        string ownerKey,
        FollowerInventoryItemViewDto item,
        string? selectedOwner,
        string? selectedItemId)
    {
        return new FollowerInventoryScreenItemViewModel(
            item.Id,
            ownerKey,
            item.TemplateId,
            FollowerInventoryItemPresentationResolver.ResolvePrimaryText(item),
            FollowerInventoryItemPresentationResolver.ResolveSecondaryText(owner, item),
            string.Equals(ownerKey, selectedOwner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Id, selectedItemId, StringComparison.Ordinal));
    }
}

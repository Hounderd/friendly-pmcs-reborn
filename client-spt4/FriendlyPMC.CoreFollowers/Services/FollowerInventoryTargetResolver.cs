using FriendlyPMC.CoreFollowers.Models;

#if SPT_CLIENT
using EFT;
using EFT.InventoryLogic;
using Comfort.Common;
#endif

namespace FriendlyPMC.CoreFollowers.Services;

public sealed record FollowerInventoryPlacementProfile(
    IReadOnlyList<string> EquipSlots,
    bool CanStoreInCarryContainers);

public sealed record FollowerInventoryTargetViewModel(
    string Key,
    string Label,
    string ToId,
    string ToContainer,
    string? ToLocationJson,
    bool IsEquipTarget,
    bool IsSelected);

public interface IFollowerInventoryTargetResolver
{
    IReadOnlyList<FollowerInventoryTargetViewModel> ResolveTargets(
        FollowerInventoryViewState state,
        string selectedOwner,
        string selectedItemId);
}

public sealed class FollowerInventoryTargetResolver : IFollowerInventoryTargetResolver
{
    private static readonly string[] CarryContainerSlots =
    [
        "Backpack",
        "TacticalVest",
        "SecuredContainer",
        "Pockets",
    ];

    public IReadOnlyList<FollowerInventoryTargetViewModel> ResolveTargets(
        FollowerInventoryViewState state,
        string selectedOwner,
        string selectedItemId)
    {
        return ResolveTargets(state, selectedOwner, selectedItemId, ResolvePlacementProfile);
    }

    public static IReadOnlyList<FollowerInventoryTargetViewModel> ResolveTargets(
        FollowerInventoryViewState state,
        string selectedOwner,
        string selectedItemId,
        Func<FollowerInventoryItemViewDto, FollowerInventoryPlacementProfile>? placementResolver)
    {
        if (!string.Equals(selectedOwner, "player", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(selectedItemId)
            || state.Player is null
            || state.Follower is null)
        {
            return Array.Empty<FollowerInventoryTargetViewModel>();
        }

        var playerItem = state.Player.Items.FirstOrDefault(item => string.Equals(item.Id, selectedItemId, StringComparison.Ordinal));
        if (playerItem is null)
        {
            return Array.Empty<FollowerInventoryTargetViewModel>();
        }

        placementResolver ??= ResolvePlacementProfile;
        var placementProfile = placementResolver(playerItem);
        var targets = new List<FollowerInventoryTargetViewModel>();
        var occupiedRootSlots = state.Follower.Items
            .Where(item => string.Equals(item.ParentId, state.Follower.RootId, StringComparison.Ordinal))
            .Select(item => item.SlotId)
            .Where(slotId => !string.IsNullOrWhiteSpace(slotId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var equipSlot in placementProfile.EquipSlots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (occupiedRootSlots.Contains(equipSlot))
            {
                continue;
            }

            targets.Add(new FollowerInventoryTargetViewModel(
                $"equip:{equipSlot}",
                $"Equip: {FollowerInventorySlotLabelFormatter.Format(equipSlot)}",
                state.Follower.RootId,
                equipSlot,
                null,
                true,
                false));
        }

        if (placementProfile.CanStoreInCarryContainers)
        {
            foreach (var container in state.Follower.Items
                         .Where(item =>
                             string.Equals(item.ParentId, state.Follower.RootId, StringComparison.Ordinal)
                             && item.SlotId is not null
                             && CarryContainerSlots.Contains(item.SlotId, StringComparer.OrdinalIgnoreCase))
                         .OrderBy(item => Array.FindIndex(
                             CarryContainerSlots,
                             slot => string.Equals(slot, item.SlotId, StringComparison.OrdinalIgnoreCase)))
                         .ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                targets.Add(new FollowerInventoryTargetViewModel(
                    $"store:{container.Id}",
                    $"Store: {FollowerInventorySlotLabelFormatter.Format(container.SlotId!)}",
                    container.Id,
                    string.Empty,
                    null,
                    false,
                    false));
            }
        }

        return targets;
    }

    public static FollowerInventoryPlacementProfile ResolvePlacementProfile(FollowerInventoryItemViewDto item)
    {
#if SPT_CLIENT
        try
        {
            if (!Singleton<ItemFactoryClass>.Instantiated)
            {
                return new FollowerInventoryPlacementProfile(Array.Empty<string>(), true);
            }

            var previewItem = Singleton<ItemFactoryClass>.Instance.CreateItem(item.Id, item.TemplateId, null);
            return previewItem switch
            {
                HeadwearItemClass => new FollowerInventoryPlacementProfile(new[] { "Headwear" }, true),
                FaceCoverItemClass => new FollowerInventoryPlacementProfile(new[] { "FaceCover" }, true),
                HeadphonesItemClass => new FollowerInventoryPlacementProfile(new[] { "Earpiece" }, true),
                ArmorItemClass => new FollowerInventoryPlacementProfile(new[] { "ArmorVest" }, true),
                VestItemClass => new FollowerInventoryPlacementProfile(new[] { "TacticalVest" }, true),
                BackpackItemClass => new FollowerInventoryPlacementProfile(new[] { "Backpack" }, true),
                ArmBandItemClass => new FollowerInventoryPlacementProfile(new[] { "ArmBand" }, true),
                KnifeItemClass => new FollowerInventoryPlacementProfile(new[] { "Scabbard" }, true),
                PistolItemClass => new FollowerInventoryPlacementProfile(new[] { "Holster" }, true),
                Weapon => new FollowerInventoryPlacementProfile(new[] { "FirstPrimaryWeapon", "SecondPrimaryWeapon" }, true),
                _ => new FollowerInventoryPlacementProfile(Array.Empty<string>(), true),
            };
        }
        catch
        {
        }
#endif

        return new FollowerInventoryPlacementProfile(Array.Empty<string>(), true);
    }
}

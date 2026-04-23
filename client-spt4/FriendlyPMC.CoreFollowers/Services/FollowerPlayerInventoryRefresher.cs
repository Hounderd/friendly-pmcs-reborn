#if SPT_CLIENT
using Comfort.Common;
using EFT.InventoryLogic;
using FriendlyPMC.CoreFollowers.Models;
using Newtonsoft.Json.Linq;

namespace FriendlyPMC.CoreFollowers.Services;

public interface IFollowerPlayerInventoryRefresher
{
    Task RefreshAfterInventoryMoveAsync(FollowerInventoryOwnerViewDto? playerInventory);
}

internal readonly record struct FollowerPlayerInventoryRefreshFailure(string Stage, string Detail);

internal static class FollowerPlayerInventoryRuntime
{
    private static WeakReference<InventoryController>? inventoryControllerReference;
    private static FollowerInventoryOwnerViewDto? latestPlayerInventorySnapshot;

    public static void Bind(InventoryController inventoryController)
    {
        inventoryControllerReference = new WeakReference<InventoryController>(inventoryController);
    }

    public static void RememberLatestSnapshot(FollowerInventoryOwnerViewDto? playerInventory)
    {
        if (playerInventory is null)
        {
            return;
        }

        latestPlayerInventorySnapshot = playerInventory;
    }

    public static InventoryController? GetBoundInventoryController()
    {
        if (inventoryControllerReference?.TryGetTarget(out var inventoryController) == true)
        {
            return inventoryController;
        }

        return null;
    }

    public static void ReplayLatestSnapshotToBoundController(Action<string>? logInfo = null)
    {
        var inventoryController = GetBoundInventoryController();
        TryBindAndReplayLatestSnapshot(inventoryController, logInfo);
    }

    public static void TryBindAndReplayLatestSnapshot(InventoryController? inventoryController, Action<string>? logInfo = null)
    {
        if (inventoryController?.Inventory is null
            || !FollowerPlayerInventorySnapshotSyncPolicy.ShouldReplayLatestSnapshotOnInventoryControllerBind(
                latestPlayerInventorySnapshot,
                inventoryController.Inventory.Stash?.Id))
        {
            return;
        }

        Bind(inventoryController);

        if (!FollowerPlayerInventoryRefresher.TryApplyMissingSnapshotItems(
                latestPlayerInventorySnapshot!,
                inventoryController,
                out var appliedItemCount,
                out var failure))
        {
            logInfo?.Invoke(
                $"Skipped replaying latest player snapshot on inventory controller bind: stage={failure.Stage}, detail={failure.Detail}");
            return;
        }

        inventoryController.Inventory.UpdateTotalWeight(EventArgs.Empty);
        inventoryController.ReportProfileUpdate();
        logInfo?.Invoke(
            $"Replayed latest player snapshot on inventory controller bind: stashRoot={latestPlayerInventorySnapshot!.RootId}, itemCount={latestPlayerInventorySnapshot.Items.Count}, appliedItems={appliedItemCount}");
        if (appliedItemCount == 0)
        {
            FollowerPlayerInventoryRefresher.LogSnapshotSyncDiagnostics(
                "inventory-controller-bind",
                latestPlayerInventorySnapshot!,
                inventoryController,
                logInfo);
        }
    }
}

internal sealed class FollowerPlayerInventoryRefresher : IFollowerPlayerInventoryRefresher
{
    private readonly Func<InventoryController?> getInventoryController;
    private readonly Action<string>? logInfo;

    public FollowerPlayerInventoryRefresher(
        Func<InventoryController?>? getInventoryController = null,
        Action<string>? logInfo = null)
    {
        this.getInventoryController = getInventoryController ?? FollowerPlayerInventoryRuntime.GetBoundInventoryController;
        this.logInfo = logInfo ?? FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo;
    }

    public Task RefreshAfterInventoryMoveAsync(FollowerInventoryOwnerViewDto? playerInventory)
    {
        if (playerInventory is null)
        {
            return Task.CompletedTask;
        }

        FollowerPlayerInventoryRuntime.RememberLatestSnapshot(playerInventory);

        var inventoryController = getInventoryController();
        if (inventoryController?.Inventory is null)
        {
            return Task.CompletedTask;
        }

        if (!TryApplyMissingSnapshotItems(playerInventory, inventoryController, out var appliedItemCount, out var failure))
        {
            logInfo?.Invoke(
                $"Skipped live player inventory refresh after follower move: stage={failure.Stage}, detail={failure.Detail}");
            return Task.CompletedTask;
        }

        inventoryController.Inventory.UpdateTotalWeight(EventArgs.Empty);
        inventoryController.ReportProfileUpdate();
        logInfo?.Invoke($"Applied live player inventory refresh after follower move: stashRoot={playerInventory.RootId}, itemCount={playerInventory.Items.Count}, appliedItems={appliedItemCount}");
        if (appliedItemCount == 0)
        {
            LogSnapshotSyncDiagnostics(
                "post-move-refresh",
                playerInventory,
                inventoryController,
                logInfo);
        }
        return Task.CompletedTask;
    }

    internal static bool TryApplyMissingSnapshotItems(
        FollowerInventoryOwnerViewDto owner,
        InventoryController inventoryController,
        out int appliedItemCount,
        out FollowerPlayerInventoryRefreshFailure failure)
    {
        appliedItemCount = 0;
        failure = default;
        if (!Singleton<ItemFactoryClass>.Instantiated)
        {
            failure = new FollowerPlayerInventoryRefreshFailure("item-factory", "ItemFactoryClass singleton is not instantiated.");
            return false;
        }

        var currentInventory = inventoryController.Inventory;
        if (currentInventory is null)
        {
            failure = new FollowerPlayerInventoryRefreshFailure("inventory", "Inventory controller is not bound to a live inventory.");
            return false;
        }

        var rootItems = EnumerateRootItems(currentInventory).ToArray();
        var currentFlatItems = Singleton<ItemFactoryClass>.Instance.TreeToFlatItems(rootItems);
        var currentItemIds = currentFlatItems
            .Select(item => item._id.ToString())
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .ToHashSet(StringComparer.Ordinal);
        var snapshotItemIds = owner.Items
            .Select(item => item.Id)
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .ToHashSet(StringComparer.Ordinal);
        var knownRootIds = new[]
        {
            currentInventory.Equipment?.Id,
            currentInventory.Stash?.Id,
            currentInventory.QuestRaidItems?.Id,
            currentInventory.QuestStashItems?.Id,
            currentInventory.SortingTable?.Id,
            currentInventory.HideoutCustomizationStash?.Id,
        };
        var removalRootIds = FollowerPlayerInventorySnapshotSyncPolicy.CollectExtraneousKnownRootSubtreeRootIds(
            currentFlatItems
                .Select(item => new FollowerPlayerInventoryNodeRef(item._id.ToString(), item.parentId?.ToString()))
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .ToArray(),
            snapshotItemIds,
            knownRootIds);
        var attachmentPlans = FollowerPlayerInventorySnapshotSyncPolicy.CollectMissingKnownRootAttachmentPlans(
            owner,
            currentItemIds,
            knownRootIds);
        if (removalRootIds.Count == 0 && attachmentPlans.Count == 0)
        {
            return true;
        }

        if (!TryRemoveExtraneousItems(removalRootIds, inventoryController, out var removedItemCount, out failure))
        {
            return false;
        }

        appliedItemCount += removedItemCount;
        foreach (var plan in attachmentPlans)
        {
            if (!TryBuildDetachedSubtree(plan, out var rootItem, out var subtreeItemCount, out failure))
            {
                return false;
            }

            if (!TryCreateAttachmentAddress(plan, currentInventory, inventoryController, rootItem, out var address, out failure))
            {
                return false;
            }

            if (!TryAddDetachedSubtree(plan, inventoryController, rootItem, address, out failure))
            {
                return false;
            }

            appliedItemCount += subtreeItemCount;
        }

        return true;
    }

    internal static void LogSnapshotSyncDiagnostics(
        string context,
        FollowerInventoryOwnerViewDto snapshot,
        InventoryController inventoryController,
        Action<string>? logInfo)
    {
        if (logInfo is null
            || !Singleton<ItemFactoryClass>.Instantiated
            || inventoryController.Inventory is null)
        {
            return;
        }

        try
        {
            var liveInventory = inventoryController.Inventory;
            var profileInventory = inventoryController.Profile?.InventoryInfo;
            var snapshotItemIds = snapshot.Items
                .Select(item => item.Id)
                .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                .ToHashSet(StringComparer.Ordinal);
            var liveItemIds = CollectFlatItemIds(liveInventory);
            var profileItemIds = profileInventory is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : CollectFlatItemIds(profileInventory);

            logInfo(
                $"Follower player inventory sync diagnostics: context={context}, snapshotCount={snapshotItemIds.Count}, liveCount={liveItemIds.Count}, profileCount={profileItemIds.Count}, liveProfileSameRef={ReferenceEquals(liveInventory, profileInventory)}, liveMinusSnapshot=[{FormatIdSetDifference(liveItemIds, snapshotItemIds)}], snapshotMinusLive=[{FormatIdSetDifference(snapshotItemIds, liveItemIds)}], profileMinusSnapshot=[{FormatIdSetDifference(profileItemIds, snapshotItemIds)}], snapshotMinusProfile=[{FormatIdSetDifference(snapshotItemIds, profileItemIds)}], liveMinusProfile=[{FormatIdSetDifference(liveItemIds, profileItemIds)}], profileMinusLive=[{FormatIdSetDifference(profileItemIds, liveItemIds)}]");
        }
        catch (Exception ex)
        {
            logInfo(
                $"Follower player inventory sync diagnostics failed: context={context}, error={ex.GetType().Name}, detail={ex.Message}");
        }
    }

    private static bool TryRemoveExtraneousItems(
        IReadOnlyList<string> removalRootIds,
        InventoryController inventoryController,
        out int removedItemCount,
        out FollowerPlayerInventoryRefreshFailure failure)
    {
        removedItemCount = 0;
        failure = default;
        foreach (var removalRootId in removalRootIds)
        {
            if (!inventoryController.TryFindItem(removalRootId, out var liveItem) || liveItem is null)
            {
                continue;
            }

            var removeResult = InteractionsHandlerClass.RemoveWithoutRestrictions(liveItem, inventoryController);
            if (removeResult.Failed)
            {
                failure = new FollowerPlayerInventoryRefreshFailure(
                    "remove-subtree",
                    $"RemoveWithoutRestrictions failed for root={removalRootId}: {removeResult.Error}");
                return false;
            }

            removeResult.Value.RaiseEvents(inventoryController, CommandStatus.Begin);
            removeResult.Value.RaiseEvents(inventoryController, CommandStatus.Succeed);
            removedItemCount++;
        }

        return true;
    }

    private static bool TryBuildDetachedSubtree(
        FollowerPlayerInventoryAttachmentPlan plan,
        out Item rootItem,
        out int subtreeItemCount,
        out FollowerPlayerInventoryRefreshFailure failure)
    {
        rootItem = null!;
        subtreeItemCount = 0;
        failure = default;
        var malformedItems = new List<string>();
        var flatItems = FollowerPlayerInventoryLiveRefreshPolicy
            .NormalizeForLiveRefresh(new FollowerInventoryOwnerViewDto("player", plan.RootItemId, plan.Items))
            .Select(item => TryCreateFlatItem(item, out var flatItem, out var error)
                ? flatItem
                : TrackMalformedItem(item, error, malformedItems))
            .Where(item => item is not null)
            .Cast<FlatItemsDataClass>()
            .ToArray();
        if (flatItems.Length == 0)
        {
            failure = new FollowerPlayerInventoryRefreshFailure(
                "build-subtree",
                $"No flat items were produced for root={plan.RootItemId}.");
            return false;
        }

        if (malformedItems.Count > 0)
        {
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Skipped malformed player inventory snapshot rows during live refresh: count={malformedItems.Count}");
        }

        var result = Singleton<ItemFactoryClass>.Instance.FlatItemsToTree(flatItems, silentMode: false);
        if (result.DeserializationErrors.Count > 0
            || !result.Items.TryGetValue(plan.RootItemId, out rootItem)
            || rootItem is null)
        {
            var errorDetail = result.DeserializationErrors.Count > 0
                ? string.Join(" | ", result.DeserializationErrors.Select(error => error.ToString()))
                : $"Detached subtree root {plan.RootItemId} was not produced by FlatItemsToTree.";
            failure = new FollowerPlayerInventoryRefreshFailure("build-subtree", errorDetail);
            return false;
        }

        subtreeItemCount = flatItems.Length;
        return true;
    }

    private static FlatItemsDataClass? TrackMalformedItem(
        FollowerInventoryItemViewDto item,
        string error,
        List<string> malformedItems)
    {
        malformedItems.Add($"{item.Id}|{item.TemplateId}|{item.ParentId}|{item.SlotId}|{error}");
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Skipping malformed player inventory snapshot item during live refresh: id={item.Id}, tpl={item.TemplateId}, parent={item.ParentId}, slot={item.SlotId}, reason={error}");
        return null;
    }

    private static bool TryCreateFlatItem(FollowerInventoryItemViewDto item, out FlatItemsDataClass flatItem, out string error)
    {
        flatItem = new FlatItemsDataClass();
        error = string.Empty;
        if (!TryParseMongoId(item.Id, out var id))
        {
            error = "invalid id";
            return false;
        }

        if (!TryParseMongoId(item.TemplateId, out var templateId))
        {
            error = "invalid template";
            return false;
        }

        EFT.MongoID? parentId = null;
        if (!string.IsNullOrWhiteSpace(item.ParentId))
        {
            if (!TryParseMongoId(item.ParentId, out var parsedParentId))
            {
                error = "invalid parent";
                return false;
            }

            parentId = parsedParentId;
        }

        flatItem = new FlatItemsDataClass
        {
            _id = id,
            _tpl = templateId,
            parentId = parentId,
            slotId = item.SlotId ?? string.Empty,
            location = CreateJsonWrapper(item.LocationJson),
            upd = CreateJsonWrapper(item.UpdJson),
        };
        return true;
    }

    private static bool TryParseMongoId(string? value, out EFT.MongoID mongoId)
    {
        mongoId = default!;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 24)
        {
            return false;
        }

        try
        {
            mongoId = new EFT.MongoID(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static GClass846? CreateJsonWrapper(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return new GClass846
        {
            JToken = JToken.Parse(json),
        };
    }

    private static bool TryCreateAttachmentAddress(
        FollowerPlayerInventoryAttachmentPlan plan,
        Inventory inventory,
        InventoryController inventoryController,
        Item rootItem,
        out ItemAddress address,
        out FollowerPlayerInventoryRefreshFailure failure)
    {
        address = null!;
        failure = default;
        if (FollowerPlayerInventorySnapshotSyncPolicy.IsStashGridAttachmentPlan(plan, inventory.Stash?.Id)
            && inventory.Stash is not null)
        {
            var location = CreateGridLocation(
                plan.LocationJson,
                rootItem,
                inventory.Stash.Grid,
                FollowerPlayerInventorySnapshotSyncPolicy.ShouldPreferFreeSpaceForLiveStashAttachment(plan, inventory.Stash.Id));
            if (location is null)
            {
                failure = new FollowerPlayerInventoryRefreshFailure(
                    "resolve-address",
                    $"Stash grid location could not be parsed for root={plan.RootItemId}, location={plan.LocationJson ?? "<null>"}.");
                return false;
            }

            address = inventory.Stash.Grid.CreateItemAddress(location);
            return true;
        }

        if (!inventoryController.TryFindItem(plan.ParentId, out var parentItem)
            || parentItem is not GClass3248 parentCompound)
        {
            failure = new FollowerPlayerInventoryRefreshFailure(
                "resolve-address",
                $"Parent item {plan.ParentId} was not found in the live inventory.");
            return false;
        }

        var container = parentCompound.Containers.FirstOrDefault(candidate => string.Equals(candidate.ID, plan.ContainerId, StringComparison.Ordinal));
        if (container is StashGridClass grid)
        {
            var location = CreateGridLocation(plan.LocationJson, rootItem, grid);
            if (location is null)
            {
                failure = new FollowerPlayerInventoryRefreshFailure(
                    "resolve-address",
                    $"Grid location could not be parsed for root={plan.RootItemId}, container={plan.ContainerId}, location={plan.LocationJson ?? "<null>"}.");
                return false;
            }

            address = grid.CreateItemAddress(location);
            return true;
        }

        if (container is Slot slot)
        {
            address = slot.CreateItemAddress();
            return true;
        }

        if (container is null
            && inventory.Stash is not null
            && string.Equals(plan.ParentId, inventory.Stash.Id, StringComparison.Ordinal)
            && string.Equals(plan.ContainerId, inventory.Stash.Grid.ID, StringComparison.Ordinal))
        {
            var location = CreateGridLocation(
                plan.LocationJson,
                rootItem,
                inventory.Stash.Grid,
                FollowerPlayerInventorySnapshotSyncPolicy.ShouldPreferFreeSpaceForLiveStashAttachment(plan, inventory.Stash.Id));
            if (location is null)
            {
                failure = new FollowerPlayerInventoryRefreshFailure(
                    "resolve-address",
                    $"Stash fallback location could not be parsed for root={plan.RootItemId}, location={plan.LocationJson ?? "<null>"}.");
                return false;
            }

            address = inventory.Stash.Grid.CreateItemAddress(location);
            return true;
        }

        var availableContainerIds = string.Join(",", parentCompound.Containers.Select(candidate => candidate.ID));
        failure = new FollowerPlayerInventoryRefreshFailure(
            "resolve-address",
            $"Container {plan.ContainerId} was not found under parent={plan.ParentId}. Available={availableContainerIds}.");
        return false;
    }

    private static bool TryAddDetachedSubtree(
        FollowerPlayerInventoryAttachmentPlan plan,
        InventoryController inventoryController,
        Item rootItem,
        ItemAddress address,
        out FollowerPlayerInventoryRefreshFailure failure)
    {
        failure = default;
        var addResult = InteractionsHandlerClass.AddWithoutRestrictions(rootItem, address, inventoryController);
        if (!addResult.Failed)
        {
            addResult.Value.RaiseEvents(inventoryController, CommandStatus.Begin);
            addResult.Value.RaiseEvents(inventoryController, CommandStatus.Succeed);
            return true;
        }

        if (address.Container is not StashGridClass stashGrid)
        {
            failure = new FollowerPlayerInventoryRefreshFailure(
                "attach-subtree",
                $"AddWithoutRestrictions failed for root={plan.RootItemId}: {addResult.Error}");
            return false;
        }

        var fallbackLocation = stashGrid.FindFreeSpace(rootItem);
        var fallbackAddress = stashGrid.CreateItemAddress(fallbackLocation);
        var fallbackResult = InteractionsHandlerClass.AddWithoutRestrictions(rootItem, fallbackAddress, inventoryController);
        if (fallbackResult.Failed)
        {
            failure = new FollowerPlayerInventoryRefreshFailure(
                "attach-subtree",
                $"AddWithoutRestrictions failed for root={plan.RootItemId}: primary={addResult.Error}; fallback={fallbackResult.Error}");
            return false;
        }

        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Applied live player inventory free-space fallback after follower move: root={plan.RootItemId}, originalLocation={plan.LocationJson ?? "<null>"}, fallbackLocation={fallbackLocation}");
        fallbackResult.Value.RaiseEvents(inventoryController, CommandStatus.Begin);
        fallbackResult.Value.RaiseEvents(inventoryController, CommandStatus.Succeed);
        return true;
    }

    private static LocationInGrid? CreateGridLocation(
        string? locationJson,
        Item rootItem,
        StashGridClass grid,
        bool preferFreeSpace = false)
    {
        if (preferFreeSpace || string.IsNullOrWhiteSpace(locationJson))
        {
            return grid.FindFreeSpace(rootItem);
        }

        var locationWrapper = CreateJsonWrapper(locationJson);
        return locationWrapper is null
            ? null
            : GClass1911.CreateItemLocation<LocationInGrid>(locationWrapper);
    }

    private static IEnumerable<Item> EnumerateRootItems(Inventory inventory)
    {
        if (inventory.Equipment is not null)
        {
            yield return inventory.Equipment;
        }

        if (inventory.Stash is not null)
        {
            yield return inventory.Stash;
        }

        if (inventory.QuestRaidItems is not null)
        {
            yield return inventory.QuestRaidItems;
        }

        if (inventory.QuestStashItems is not null)
        {
            yield return inventory.QuestStashItems;
        }

        if (inventory.SortingTable is not null)
        {
            yield return inventory.SortingTable;
        }

        if (inventory.HideoutCustomizationStash is not null)
        {
            yield return inventory.HideoutCustomizationStash;
        }

        if (inventory.HideoutAreaStashes is null)
        {
            yield break;
        }

        foreach (var stash in inventory.HideoutAreaStashes.Values)
        {
            if (stash is not null)
            {
                yield return stash;
            }
        }
    }

    private static HashSet<string> CollectFlatItemIds(Inventory inventory)
    {
        return Singleton<ItemFactoryClass>.Instance.TreeToFlatItems(EnumerateRootItems(inventory).ToArray())
            .Select(item => item._id.ToString())
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FormatIdSetDifference(ISet<string> source, ISet<string> target, int maxItems = 8)
    {
        var values = source
            .Where(itemId => !target.Contains(itemId))
            .OrderBy(itemId => itemId, StringComparer.Ordinal)
            .Take(maxItems)
            .ToArray();
        return values.Length == 0
            ? "<none>"
            : string.Join(", ", values);
    }
}
#else
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public interface IFollowerPlayerInventoryRefresher
{
    Task RefreshAfterInventoryMoveAsync(FollowerInventoryOwnerViewDto? playerInventory);
}

internal sealed class FollowerPlayerInventoryRefresher : IFollowerPlayerInventoryRefresher
{
    public Task RefreshAfterInventoryMoveAsync(FollowerInventoryOwnerViewDto? playerInventory)
    {
        return Task.CompletedTask;
    }
}
#endif

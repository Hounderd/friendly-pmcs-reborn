#if SPT_CLIENT
using Comfort.Common;
using EFT.InventoryLogic;
using FriendlyPMC.CoreFollowers.Models;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace FriendlyPMC.CoreFollowers.Services;

public interface IFollowerPlayerInventoryRefresher
{
    Task RefreshAfterInventoryMoveAsync(FollowerInventoryOwnerViewDto? playerInventory);
}

internal static class FollowerPlayerInventoryRuntime
{
    private static WeakReference<InventoryController>? inventoryControllerReference;

    public static void Bind(InventoryController inventoryController)
    {
        inventoryControllerReference = new WeakReference<InventoryController>(inventoryController);
    }

    public static InventoryController? GetBoundInventoryController()
    {
        if (inventoryControllerReference?.TryGetTarget(out var inventoryController) == true)
        {
            return inventoryController;
        }

        return null;
    }
}

internal sealed class FollowerPlayerInventoryRefresher : IFollowerPlayerInventoryRefresher
{
    private readonly Func<InventoryController?> getInventoryController;
    private readonly Func<FollowerInventoryOwnerViewDto, StashItemClass?> buildStashFromSnapshot;
    private readonly Action<string>? logInfo;

    public FollowerPlayerInventoryRefresher(
        Func<InventoryController?>? getInventoryController = null,
        Func<FollowerInventoryOwnerViewDto, StashItemClass?>? buildStashFromSnapshot = null,
        Action<string>? logInfo = null)
    {
        this.getInventoryController = getInventoryController ?? FollowerPlayerInventoryRuntime.GetBoundInventoryController;
        this.buildStashFromSnapshot = buildStashFromSnapshot ?? BuildStashFromSnapshot;
        this.logInfo = logInfo ?? FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo;
    }

    public Task RefreshAfterInventoryMoveAsync(FollowerInventoryOwnerViewDto? playerInventory)
    {
        if (playerInventory is null)
        {
            return Task.CompletedTask;
        }

        var inventoryController = getInventoryController();
        if (inventoryController?.Inventory is null)
        {
            return Task.CompletedTask;
        }

        var stash = buildStashFromSnapshot(playerInventory);
        if (stash is null)
        {
            logInfo?.Invoke("Skipped live player inventory refresh after follower move: player stash snapshot could not be rebuilt.");
            return Task.CompletedTask;
        }

        inventoryController.Inventory.Stash = stash;
        if (inventoryController.Profile is EFT.Profile profile && profile.Inventory is not null)
        {
            profile.Inventory.Stash = stash;
            profile.Inventory.UpdateTotalWeight(EventArgs.Empty);
        }

        inventoryController.Inventory.UpdateTotalWeight(EventArgs.Empty);
        inventoryController.ReportProfileUpdate();
        logInfo?.Invoke($"Applied live player inventory refresh after follower move: stashRoot={playerInventory.RootId}, itemCount={playerInventory.Items.Count}");
        return Task.CompletedTask;
    }

    internal static StashItemClass? BuildStashFromSnapshot(FollowerInventoryOwnerViewDto owner)
    {
        if (!Singleton<ItemFactoryClass>.Instantiated)
        {
            return null;
        }

        var malformedItems = new List<string>();
        var flatItems = FollowerPlayerInventorySnapshotSyncPolicy
            .CollectRootSubtree(owner)
            .Select(item => TryCreateFlatItem(item, out var flatItem, out var error)
                ? flatItem
                : TrackMalformedItem(item, error, malformedItems))
            .Where(item => item is not null)
            .Cast<FlatItemsDataClass>()
            .ToArray();
        if (flatItems.Length == 0)
        {
            return null;
        }

        if (malformedItems.Count > 0)
        {
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Skipped malformed player inventory snapshot rows during live refresh: count={malformedItems.Count}");
        }

        var tree = Singleton<ItemFactoryClass>.Instance.FlatItemsToTree(
            flatItems,
            true,
            new Dictionary<string, Item>(StringComparer.Ordinal));
        return tree.Items.TryGetValue(owner.RootId, out var rootItem)
            ? rootItem as StashItemClass
            : null;
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

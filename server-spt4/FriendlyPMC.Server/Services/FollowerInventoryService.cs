using System.Text.Json;
using System.Text.Json.Serialization;
using FriendlyPMC.Server.Models;
using FriendlyPMC.Server.Models.Requests;
using FriendlyPMC.Server.Models.Responses;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Servers;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerInventoryService(
    FollowerRosterStore store,
    ProfileHelper? profileHelper = null,
    DatabaseService? databaseService = null,
    SaveServer? saveServer = null,
    FollowerDiagnosticsLog? diagnosticsLog = null)
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public sealed record PreviewResult(
        bool Succeeded,
        string? ErrorMessage,
        IReadOnlyList<Item> PlayerItems,
        FollowerProfileSnapshot FollowerProfile);

    private sealed record EquippedItemSwapPlan(
        IReadOnlyList<Item> Subtree,
        FollowerInventoryMoveRequest Request);

    public static PreviewResult PreviewMove(
        PmcData playerProfile,
        FollowerProfileSnapshot followerProfile,
        IReadOnlyDictionary<string, TemplateItem> itemTemplates,
        FollowerInventoryMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(playerProfile);
        ArgumentNullException.ThrowIfNull(followerProfile);
        ArgumentNullException.ThrowIfNull(itemTemplates);
        ArgumentNullException.ThrowIfNull(request);

        var playerItems = CloneItems(playerProfile.Inventory?.Items ?? []);
        var followerInventory = followerProfile.Inventory ?? FollowerInventoryMigrationPolicy.CreateInventorySnapshot(followerProfile.Equipment);
        if (followerInventory is null)
        {
            return new PreviewResult(false, "Follower inventory is unavailable.", playerItems, followerProfile);
        }

        var followerItems = CloneItems(followerInventory.Items.Select(FollowerProfileFactory.CreateInventoryItem));
        NormalizeAllIndexedSiblingLocations(playerItems);
        NormalizeAllIndexedSiblingLocations(followerItems);
        var sourceIsPlayer = string.Equals(request.SourceOwner, "player", StringComparison.OrdinalIgnoreCase);
        var sourceItems = sourceIsPlayer ? playerItems : followerItems;
        var targetItems = sourceIsPlayer ? followerItems : playerItems;
        var sourceRoot = sourceItems.FirstOrDefault(item => MatchesId(item.Id, request.ItemId));
        if (sourceRoot is null)
        {
            return new PreviewResult(false, "Source item was not found.", playerItems, followerProfile);
        }

        var effectiveRequest = FollowerInventoryValidationPolicy.ResolveAutoPlacement(
            targetItems,
            followerInventory.EquipmentId,
            itemTemplates,
            request,
            sourceRoot,
            targetIsFollower: sourceIsPlayer);
        var moveSubtree = CollectSubtree(sourceItems, request.ItemId);
        var validationError = FollowerInventoryValidationPolicy.ValidateTarget(
            targetItems,
            followerInventory.EquipmentId,
            itemTemplates,
            effectiveRequest,
            sourceRoot,
            targetIsFollower: sourceIsPlayer);
        if (validationError is not null)
        {
            return new PreviewResult(false, validationError, playerItems, followerProfile);
        }

        string? swapError = null;
        var playerStashRootId = ResolvePlayerStashRootId(playerProfile);
        var playerStashContainerKey = ResolvePlayerStashContainerKey(playerItems, itemTemplates, playerStashRootId);
        var equippedItemSwapPlans = sourceIsPlayer
            ? PlanEquippedItemSwap(
                playerItems,
                targetItems,
                playerStashRootId,
                playerStashContainerKey,
                followerInventory.EquipmentId,
                itemTemplates,
                request,
                sourceRoot,
                moveSubtree,
                effectiveRequest,
                out swapError)
            : new List<EquippedItemSwapPlan>();
        if (!string.IsNullOrWhiteSpace(swapError))
        {
            return new PreviewResult(false, swapError, playerItems, followerProfile);
        }

        var originalParentId = sourceRoot.ParentId;
        var originalSlotId = sourceRoot.SlotId;
        sourceItems.RemoveAll(item => moveSubtree.Any(child => child.Id == item.Id));
        NormalizeIndexedSiblingLocations(sourceItems, originalParentId, originalSlotId);

        foreach (var swapPlan in equippedItemSwapPlans)
        {
            targetItems.RemoveAll(item => swapPlan.Subtree.Any(child => child.Id == item.Id));
            var swappedRoot = swapPlan.Subtree[0];
            swappedRoot.ParentId = swapPlan.Request.ToId;
            swappedRoot.SlotId = swapPlan.Request.ToContainer;
            swappedRoot.Location = DeserializeLocation(swapPlan.Request.ToLocationJson);
            playerItems.AddRange(swapPlan.Subtree);
        }

        var movedRoot = moveSubtree[0];
        movedRoot.ParentId = effectiveRequest.ToId;
        movedRoot.SlotId = effectiveRequest.ToContainer;
        movedRoot.Location = DeserializeLocation(effectiveRequest.ToLocationJson);
        targetItems.AddRange(moveSubtree);
        NormalizeAllIndexedSiblingLocations(playerItems);

        var updatedFollowerItems = sourceIsPlayer ? targetItems : sourceItems;
        NormalizeAllIndexedSiblingLocations(updatedFollowerItems);
        var updatedFollowerSnapshot = followerProfile with
        {
            Inventory = new FollowerInventorySnapshot(
                followerInventory.EquipmentId,
                updatedFollowerItems
                    .Select(CreateSnapshotItem)
                    .ToArray()),
        };

        return new PreviewResult(true, null, playerItems, FollowerInventoryMigrationPolicy.Upgrade(updatedFollowerSnapshot));
    }

    public async Task<FollowerInventoryMoveResponse> MoveAsync(string sessionId, FollowerInventoryMoveRequest request)
    {
        if (profileHelper is null || databaseService is null)
        {
            throw new InvalidOperationException("Follower inventory transfers are unavailable because required SPT services are not registered.");
        }

        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var loadedProfiles = (await store.LoadProfilesAsync(resolvedSessionId)).ToList();
        var (profiles, profilesChanged) = FollowerInventoryIdIntegrityPolicy.NormalizeFollowerProfiles(loadedProfiles);
        var profileIndex = profiles.FindIndex(profile => string.Equals(profile.Aid, request.FollowerAid, StringComparison.Ordinal));
        if (profileIndex < 0)
        {
            return new FollowerInventoryMoveResponse(false, "Follower was not found.", null);
        }

        var playerProfile = profileHelper.GetPmcProfile(new MongoId(sessionId));
        if (playerProfile?.Inventory?.Items is null)
        {
            return new FollowerInventoryMoveResponse(false, "Player inventory is unavailable.", null);
        }

        var templates = databaseService.GetItems()
            .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value, StringComparer.Ordinal);
        var playerInventoryChanged = FollowerInventoryIdIntegrityPolicy.NormalizePlayerProfileInventory(playerProfile, templates);

        if (profilesChanged || playerInventoryChanged)
        {
            await store.SaveProfilesAsync(resolvedSessionId, profiles);
            if (saveServer is not null)
            {
                await saveServer.SaveProfileAsync(new MongoId(sessionId));
            }
        }

        var originalPlayerItems = CloneItems(playerProfile.Inventory.Items);
        var originalFollowerProfile = profiles[profileIndex];
        var preview = PreviewMove(playerProfile, originalFollowerProfile, templates, request);
        if (!preview.Succeeded)
        {
            diagnosticsLog?.Append($"inventory-move session={sessionId} follower={request.FollowerAid} source={request.SourceOwner} item={request.ItemId} result=rejected reason={preview.ErrorMessage}");
            return new FollowerInventoryMoveResponse(false, preview.ErrorMessage, originalFollowerProfile.Inventory);
        }

        try
        {
            playerProfile.Inventory.Items = preview.PlayerItems.ToList();
            profiles[profileIndex] = preview.FollowerProfile;
            await store.SaveProfilesAsync(resolvedSessionId, profiles);
            if (saveServer is not null)
            {
                await saveServer.SaveProfileAsync(new MongoId(sessionId));
            }

            diagnosticsLog?.Append($"inventory-move session={sessionId} follower={request.FollowerAid} source={request.SourceOwner} item={request.ItemId} result=success");
            return new FollowerInventoryMoveResponse(true, null, preview.FollowerProfile.Inventory);
        }
        catch
        {
            playerProfile.Inventory.Items = originalPlayerItems.ToList();
            profiles[profileIndex] = originalFollowerProfile;
            throw;
        }
    }

    public async Task<GetFollowerInventoryResponse?> GetInventoryAsync(string sessionId, string followerAid)
    {
        try
        {
            if (profileHelper is null || databaseService is null)
            {
                throw new InvalidOperationException("Follower inventory views are unavailable because required SPT services are not registered.");
            }

        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        diagnosticsLog?.Append($"inventory-get session={sessionId} resolved={resolvedSessionId} follower={followerAid} result=start");
        var loadedProfiles = (await store.LoadProfilesAsync(resolvedSessionId)).ToList();
        var (profiles, profilesChanged) = FollowerInventoryIdIntegrityPolicy.NormalizeFollowerProfiles(loadedProfiles);
        var profileIndex = profiles.FindIndex(profile => string.Equals(profile.Aid, followerAid, StringComparison.Ordinal));
        var followerProfile = profileIndex >= 0 ? profiles[profileIndex] : null;
        if (followerProfile is null)
        {
            diagnosticsLog?.Append($"inventory-get session={sessionId} resolved={resolvedSessionId} follower={followerAid} result=follower-miss");
            return null;
        }

            var playerProfile = profileHelper.GetPmcProfile(new MongoId(sessionId));
        if (playerProfile is null)
        {
            diagnosticsLog?.Append($"inventory-get session={sessionId} resolved={resolvedSessionId} follower={followerAid} result=player-miss");
            return null;
        }

        if (playerProfile.Inventory?.Items is null)
        {
            diagnosticsLog?.Append($"inventory-get session={sessionId} resolved={resolvedSessionId} follower={followerAid} result=player-items-miss");
            return null;
        }

        var templates = databaseService.GetItems()
            .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value, StringComparer.Ordinal);
        var duplicatePlayerIdsChanged = FollowerInventoryIdIntegrityPolicy.NormalizePlayerProfileInventory(playerProfile, templates);
        var playerInventoryChanged = duplicatePlayerIdsChanged || NormalizeAllIndexedSiblingLocations(playerProfile.Inventory.Items);
        var followerInventory = followerProfile.Inventory ?? FollowerInventoryMigrationPolicy.CreateInventorySnapshot(followerProfile.Equipment);
        var followerInventoryItems = CloneItems(followerInventory?.Items.Select(FollowerProfileFactory.CreateInventoryItem) ?? []);
        var followerInventoryChanged = NormalizeAllIndexedSiblingLocations(followerInventoryItems);
        if (followerInventoryChanged)
        {
            followerProfile = followerProfile with
            {
                Inventory = new FollowerInventorySnapshot(
                    followerInventory?.EquipmentId ?? string.Empty,
                    followerInventoryItems.Select(CreateSnapshotItem).ToArray()),
            };
            profiles[profileIndex] = FollowerInventoryMigrationPolicy.Upgrade(followerProfile);
        }

        if (profilesChanged || playerInventoryChanged || followerInventoryChanged)
        {
            if (profilesChanged || followerInventoryChanged)
            {
                await store.SaveProfilesAsync(resolvedSessionId, profiles);
            }

            if (saveServer is not null)
            {
                await saveServer.SaveProfileAsync(new MongoId(sessionId));
            }

            diagnosticsLog?.Append(
                $"inventory-heal session={sessionId} resolved={resolvedSessionId} follower={followerAid} playerChanged={playerInventoryChanged} followerChanged={followerInventoryChanged} profileChanged={profilesChanged}");
        }

        var response = BuildInventoryView(playerProfile, followerProfile);
        diagnosticsLog?.Append(
            $"inventory-get session={sessionId} resolved={resolvedSessionId} follower={followerAid} result=success playerItems={response.Player?.Items.Count ?? 0} followerItems={response.Follower?.Items.Count ?? 0} playerRoot={response.Player?.RootId ?? "<blank>"} followerRoot={response.Follower?.RootId ?? "<blank>"}");
        return response;
        }
        catch (Exception ex)
        {
            diagnosticsLog?.Append($"inventory-get session={sessionId} follower={followerAid} result=error error={ex.GetType().Name}:{ex.Message}");
            throw;
        }
    }

    public static GetFollowerInventoryResponse BuildInventoryView(PmcData playerProfile, FollowerProfileSnapshot followerProfile)
    {
        var playerRootId = playerProfile.Inventory?.Stash?.ToString()
            ?? playerProfile.Inventory?.Equipment?.ToString()
            ?? string.Empty;
        var playerItems = (playerProfile.Inventory?.Items ?? [])
            .Select(CreateSnapshotItem)
            .ToArray();
        var followerInventory = followerProfile.Inventory ?? FollowerInventoryMigrationPolicy.CreateInventorySnapshot(followerProfile.Equipment);
        var followerItems = followerInventory?.Items ?? Array.Empty<FollowerInventoryItemSnapshot>();

        return new GetFollowerInventoryResponse(
            followerProfile.Aid,
            followerProfile.Nickname,
            new FollowerInventoryOwnerViewResponse("player", playerRootId, playerItems),
            new FollowerInventoryOwnerViewResponse("follower", followerInventory?.EquipmentId ?? string.Empty, followerItems));
    }

    private async Task<string> ResolveStorageSessionIdAsync(string sessionId)
    {
        var roster = await store.LoadRosterAsync(sessionId);
        if (roster.Count > 0)
        {
            return sessionId;
        }

        var knownSessionIds = store.GetKnownSessionIds();
        if (knownSessionIds.Count != 1)
        {
            return sessionId;
        }

        return knownSessionIds[0];
    }

    private static List<Item> CollectSubtree(List<Item> sourceItems, string rootItemId)
    {
        var sourceByParentId = sourceItems
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key!, group => group.ToArray(), StringComparer.Ordinal);
        var ordered = new List<Item>();
        if (sourceItems.FirstOrDefault(item => MatchesId(item.Id, rootItemId)) is not { } root)
        {
            return ordered;
        }

        var queue = new Queue<Item>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);
            if (sourceByParentId.TryGetValue(current.Id.ToString(), out var children))
            {
                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        return ordered;
    }

    private static List<Item> CloneItems(IEnumerable<Item> items)
    {
        return items
            .Select(CloneItem)
            .ToList();
    }

    private static Item CloneItem(Item item)
    {
        return new Item
        {
            Id = new MongoId(item.Id.ToString()),
            Template = new MongoId(item.Template.ToString()),
            ParentId = item.ParentId,
            SlotId = item.SlotId,
            Location = DeserializeLocation(SerializeOptionalJson(item.Location)),
            Desc = item.Desc,
            Upd = item.Upd is null
                ? null
                : JsonSerializer.Deserialize<Upd>(JsonSerializer.Serialize(item.Upd)),
            ExtensionData = null,
        };
    }

    private static FollowerInventoryItemSnapshot CreateSnapshotItem(Item item)
    {
        return new FollowerInventoryItemSnapshot(
            item.Id.ToString(),
            item.Template.ToString(),
            NullIfEmpty(item.ParentId),
            NullIfEmpty(item.SlotId),
            SerializeOptionalJson(item.Location),
            SerializeOptionalJson(item.Upd));
    }

    private static object? DeserializeLocation(string? locationJson)
    {
        if (string.IsNullOrWhiteSpace(locationJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(locationJson);
        return document.RootElement.Clone();
    }

    private static string? SerializeOptionalJson(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            JsonElement jsonElement => jsonElement.GetRawText(),
            _ => JsonSerializer.Serialize(value, SnapshotJsonOptions),
        };
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool MatchesId(MongoId value, string requestedId)
    {
        return string.Equals(value.ToString(), requestedId, StringComparison.Ordinal);
    }

    private static List<EquippedItemSwapPlan> PlanEquippedItemSwap(
        List<Item> playerItems,
        List<Item> followerItems,
        string playerStashRootId,
        string playerStashContainerKey,
        string followerEquipmentRootId,
        IReadOnlyDictionary<string, TemplateItem> itemTemplates,
        FollowerInventoryMoveRequest originalRequest,
        Item sourceRoot,
        List<Item> moveSubtree,
        FollowerInventoryMoveRequest effectiveRequest,
        out string? errorMessage)
    {
        errorMessage = null;
        if (!string.Equals(effectiveRequest.ToId, followerEquipmentRootId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(effectiveRequest.ToContainer)
            || string.IsNullOrWhiteSpace(playerStashRootId)
            || string.IsNullOrWhiteSpace(playerStashContainerKey))
        {
            return [];
        }

        var occupiedRoots = followerItems
            .Where(item =>
                string.Equals(item.ParentId, followerEquipmentRootId, StringComparison.Ordinal)
                && string.Equals(item.SlotId, effectiveRequest.ToContainer, StringComparison.Ordinal))
            .ToArray();
        if (occupiedRoots.Length == 0)
        {
            return [];
        }

        var simulatedPlayerItems = CloneItems(playerItems);
        simulatedPlayerItems.RemoveAll(item => moveSubtree.Any(child => child.Id == item.Id));
        NormalizeIndexedSiblingLocations(simulatedPlayerItems, sourceRoot.ParentId, sourceRoot.SlotId);

        var plans = new List<EquippedItemSwapPlan>(occupiedRoots.Length);
        foreach (var occupiedRoot in occupiedRoots)
        {
            var swapRequest = new FollowerInventoryMoveRequest(
                originalRequest.FollowerAid,
                "follower",
                occupiedRoot.Id.ToString(),
                playerStashRootId,
                playerStashContainerKey,
                null);
            var resolvedSwapRequest = FollowerInventoryValidationPolicy.ResolveAutoPlacement(
                simulatedPlayerItems,
                followerEquipmentRootId,
                itemTemplates,
                swapRequest,
                occupiedRoot,
                targetIsFollower: false);
            var swapValidationError = FollowerInventoryValidationPolicy.ValidateTarget(
                simulatedPlayerItems,
                followerEquipmentRootId,
                itemTemplates,
                resolvedSwapRequest,
                occupiedRoot,
                targetIsFollower: false);
            if (swapValidationError is not null)
            {
                errorMessage = "No space available to swap equipped item.";
                return [];
            }

            var subtree = CollectSubtree(followerItems, occupiedRoot.Id.ToString());
            var simulatedSubtree = CloneItems(subtree);
            var simulatedRoot = simulatedSubtree[0];
            simulatedRoot.ParentId = resolvedSwapRequest.ToId;
            simulatedRoot.SlotId = resolvedSwapRequest.ToContainer;
            simulatedRoot.Location = DeserializeLocation(resolvedSwapRequest.ToLocationJson);
            simulatedPlayerItems.AddRange(simulatedSubtree);

            plans.Add(new EquippedItemSwapPlan(subtree, resolvedSwapRequest));
        }

        return plans;
    }

    private static string ResolvePlayerStashRootId(PmcData playerProfile)
    {
        return playerProfile.Inventory?.Stash?.ToString()
            ?? playerProfile.Inventory?.Equipment?.ToString()
            ?? string.Empty;
    }

    private static string ResolvePlayerStashContainerKey(
        IReadOnlyList<Item> playerItems,
        IReadOnlyDictionary<string, TemplateItem> itemTemplates,
        string playerStashRootId)
    {
        if (string.IsNullOrWhiteSpace(playerStashRootId))
        {
            return string.Empty;
        }

        var stashRoot = playerItems.FirstOrDefault(item => string.Equals(item.Id.ToString(), playerStashRootId, StringComparison.Ordinal));
        if (stashRoot is null || !itemTemplates.TryGetValue(stashRoot.Template.ToString(), out var stashTemplate))
        {
            return string.Empty;
        }

        foreach (var preferredKey in new[] { "hideout", "main" })
        {
            if (TryResolveGridKey(stashTemplate, preferredKey, out var resolvedKey))
            {
                return resolvedKey;
            }
        }

        return stashTemplate.Properties?.Grids?
            .Select(grid => !string.IsNullOrWhiteSpace(grid.Name) ? grid.Name : grid.Id)
            .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key))
            ?? string.Empty;
    }

    private static bool TryResolveGridKey(TemplateItem template, string requestedKey, out string resolvedKey)
    {
        resolvedKey = template.Properties?.Grids?
            .Select(grid => !string.IsNullOrWhiteSpace(grid.Name) ? grid.Name : grid.Id)
            .FirstOrDefault(key => string.Equals(key, requestedKey, StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
        return !string.IsNullOrWhiteSpace(resolvedKey);
    }

    private static void NormalizeIndexedSiblingLocations(List<Item> items, string? parentId, string? slotId)
    {
        if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(slotId))
        {
            return;
        }

        var indexedSiblings = items
            .Where(item =>
                string.Equals(item.ParentId, parentId, StringComparison.Ordinal)
                && string.Equals(item.SlotId, slotId, StringComparison.Ordinal))
            .Select(item => new { Item = item, Index = TryReadIndexedLocation(item.Location) })
            .Where(entry => entry.Index.HasValue)
            .OrderBy(entry => entry.Index!.Value)
            .ToArray();
        if (indexedSiblings.Length == 0)
        {
            return;
        }

        for (var index = 0; index < indexedSiblings.Length; index++)
        {
            indexedSiblings[index].Item.Location = index;
        }
    }

    private static bool NormalizeAllIndexedSiblingLocations(List<Item> items)
    {
        var changed = false;
        var siblingGroups = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId) && !string.IsNullOrWhiteSpace(item.SlotId))
            .GroupBy(item => $"{item.ParentId}\n{item.SlotId}", StringComparer.Ordinal);
        foreach (var siblingGroup in siblingGroups)
        {
            var entries = siblingGroup
                .Select((item, order) => new
                {
                    Item = item,
                    Order = order,
                    Index = TryReadIndexedLocation(item.Location),
                    IsIndexedLocationCompatible = IsIndexedLocationCompatible(item.Location),
                })
                .ToArray();
            if (entries.Length < 2 || entries.Any(entry => !entry.IsIndexedLocationCompatible))
            {
                continue;
            }

            var orderedEntries = entries
                .OrderBy(entry => entry.Index ?? int.MaxValue)
                .ThenBy(entry => entry.Order)
                .ToArray();
            for (var index = 0; index < orderedEntries.Length; index++)
            {
                if (orderedEntries[index].Index != index)
                {
                    changed = true;
                }

                orderedEntries[index].Item.Location = index;
            }
        }

        return changed;
    }

    private static int? TryReadIndexedLocation(object? location)
    {
        return location switch
        {
            null => null,
            int value => value,
            long value => checked((int)value),
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var value) => value,
            _ => null,
        };
    }

    private static bool IsIndexedLocationCompatible(object? location)
    {
        return location is null || TryReadIndexedLocation(location).HasValue;
    }
}

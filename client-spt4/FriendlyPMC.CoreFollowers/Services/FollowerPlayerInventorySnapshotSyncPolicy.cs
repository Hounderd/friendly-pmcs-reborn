using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed record FollowerPlayerInventoryAttachmentPlan(
    string RootItemId,
    string ParentId,
    string ContainerId,
    string? LocationJson,
    IReadOnlyList<FollowerInventoryItemViewDto> Items);

internal sealed record FollowerPlayerInventoryNodeRef(string Id, string? ParentId);

internal static class FollowerPlayerInventorySnapshotSyncPolicy
{
    public static IReadOnlyList<FollowerInventoryItemViewDto> CollectRootSubtree(FollowerInventoryOwnerViewDto? owner)
    {
        return CollectKnownRootSubtrees(owner, owner?.RootId);
    }

    public static IReadOnlyList<FollowerInventoryItemViewDto> CollectKnownRootSubtrees(
        FollowerInventoryOwnerViewDto? owner,
        params string?[] rootIds)
    {
        if (owner is null
            || rootIds.Length == 0
            || owner.Items.Count == 0)
        {
            return Array.Empty<FollowerInventoryItemViewDto>();
        }

        var itemsByParent = owner.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var itemsById = owner.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var collected = new List<FollowerInventoryItemViewDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rootId in rootIds
                     .Where(rootId => !string.IsNullOrWhiteSpace(rootId))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!itemsById.TryGetValue(rootId!, out var rootItem))
            {
                continue;
            }

            var queue = new Queue<string>();
            queue.Enqueue(rootItem.Id);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (!itemsById.TryGetValue(currentId, out var currentItem) || !seen.Add(currentId))
                {
                    continue;
                }

                collected.Add(currentItem);
                if (!itemsByParent.TryGetValue(currentId, out var children))
                {
                    continue;
                }

                foreach (var child in children.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(child.Id) || seen.Contains(child.Id))
                    {
                        continue;
                    }

                    queue.Enqueue(child.Id);
                }
            }
        }

        return collected;
    }

    public static IReadOnlyList<FollowerInventoryItemViewDto> CollectMissingKnownRootSubtrees(
        FollowerInventoryOwnerViewDto? owner,
        ISet<string> currentItemIds,
        params string?[] rootIds)
    {
        if (currentItemIds is null)
        {
            throw new ArgumentNullException(nameof(currentItemIds));
        }

        return CollectKnownRootSubtrees(owner, rootIds)
            .Where(item => !currentItemIds.Contains(item.Id))
            .ToArray();
    }

    public static IReadOnlyList<FollowerPlayerInventoryAttachmentPlan> CollectMissingKnownRootAttachmentPlans(
        FollowerInventoryOwnerViewDto? owner,
        ISet<string> currentItemIds,
        params string?[] rootIds)
    {
        if (currentItemIds is null)
        {
            throw new ArgumentNullException(nameof(currentItemIds));
        }

        var knownItems = CollectKnownRootSubtrees(owner, rootIds);
        if (knownItems.Count == 0)
        {
            return Array.Empty<FollowerPlayerInventoryAttachmentPlan>();
        }

        var itemsById = knownItems.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var itemsByParent = knownItems
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var plans = new List<FollowerPlayerInventoryAttachmentPlan>();
        foreach (var attachableRoot in knownItems)
        {
            if (string.IsNullOrWhiteSpace(attachableRoot.Id)
                || string.IsNullOrWhiteSpace(attachableRoot.ParentId)
                || string.IsNullOrWhiteSpace(attachableRoot.SlotId)
                || currentItemIds.Contains(attachableRoot.Id)
                || !currentItemIds.Contains(attachableRoot.ParentId))
            {
                continue;
            }

            var subtreeItems = CollectDetachedSubtreeItems(attachableRoot, currentItemIds, itemsById, itemsByParent);
            if (subtreeItems.Count == 0)
            {
                continue;
            }

            plans.Add(new FollowerPlayerInventoryAttachmentPlan(
                attachableRoot.Id,
                attachableRoot.ParentId,
                attachableRoot.SlotId,
                attachableRoot.LocationJson,
                subtreeItems));
        }

        return plans;
    }

    public static bool IsStashGridAttachmentPlan(FollowerPlayerInventoryAttachmentPlan plan, string? stashRootId)
    {
        return !string.IsNullOrWhiteSpace(stashRootId)
            && string.Equals(plan.ParentId, stashRootId, StringComparison.Ordinal)
            && (string.Equals(plan.ContainerId, "hideout", StringComparison.OrdinalIgnoreCase)
                || string.Equals(plan.ContainerId, "main", StringComparison.OrdinalIgnoreCase));
    }

    public static bool ShouldPreferFreeSpaceForLiveStashAttachment(
        FollowerPlayerInventoryAttachmentPlan plan,
        string? stashRootId)
    {
        return IsStashGridAttachmentPlan(plan, stashRootId);
    }

    public static bool ShouldReplayLatestSnapshotOnInventoryControllerBind(
        FollowerInventoryOwnerViewDto? latestSnapshot,
        string? boundStashRootId)
    {
        return latestSnapshot is not null
            && !string.IsNullOrWhiteSpace(latestSnapshot.RootId)
            && !string.IsNullOrWhiteSpace(boundStashRootId)
            && string.Equals(latestSnapshot.RootId, boundStashRootId, StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> CollectExtraneousKnownRootSubtreeRootIds(
        IReadOnlyCollection<FollowerPlayerInventoryNodeRef> currentItems,
        ISet<string> snapshotItemIds,
        params string?[] rootIds)
    {
        if (currentItems is null)
        {
            throw new ArgumentNullException(nameof(currentItems));
        }

        if (snapshotItemIds is null)
        {
            throw new ArgumentNullException(nameof(snapshotItemIds));
        }

        if (currentItems.Count == 0 || rootIds.Length == 0)
        {
            return Array.Empty<string>();
        }

        var currentItemsById = currentItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var childrenByParent = currentItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

        var removalRoots = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rootId in rootIds
                     .Where(rootId => !string.IsNullOrWhiteSpace(rootId))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!currentItemsById.ContainsKey(rootId!))
            {
                continue;
            }

            var queue = new Queue<string>();
            queue.Enqueue(rootId!);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (!visited.Add(currentId))
                {
                    continue;
                }

                if (!childrenByParent.TryGetValue(currentId, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    if (snapshotItemIds.Contains(child.Id))
                    {
                        queue.Enqueue(child.Id);
                        continue;
                    }

                    removalRoots.Add(child.Id);
                }
            }
        }

        return removalRoots;
    }

    private static IReadOnlyList<FollowerInventoryItemViewDto> CollectDetachedSubtreeItems(
        FollowerInventoryItemViewDto attachableRoot,
        ISet<string> currentItemIds,
        IReadOnlyDictionary<string, FollowerInventoryItemViewDto> itemsById,
        IReadOnlyDictionary<string, FollowerInventoryItemViewDto[]> itemsByParent)
    {
        var collected = new List<FollowerInventoryItemViewDto>();
        var queue = new Queue<string>();
        queue.Enqueue(attachableRoot.Id);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!itemsById.TryGetValue(currentId, out var currentItem)
                || currentItemIds.Contains(currentId))
            {
                continue;
            }

            collected.Add(string.Equals(currentId, attachableRoot.Id, StringComparison.Ordinal)
                ? currentItem with
                {
                    ParentId = null,
                    SlotId = null,
                    LocationJson = null,
                }
                : currentItem);

            if (!itemsByParent.TryGetValue(currentId, out var children))
            {
                continue;
            }

            foreach (var child in children.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(child.Id) || currentItemIds.Contains(child.Id))
                {
                    continue;
                }

                queue.Enqueue(child.Id);
            }
        }

        return collected;
    }
}

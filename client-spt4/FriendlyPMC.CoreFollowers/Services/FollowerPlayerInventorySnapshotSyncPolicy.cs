using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerPlayerInventorySnapshotSyncPolicy
{
    public static IReadOnlyList<FollowerInventoryItemViewDto> CollectRootSubtree(FollowerInventoryOwnerViewDto? owner)
    {
        if (owner is null
            || string.IsNullOrWhiteSpace(owner.RootId)
            || owner.Items.Count == 0)
        {
            return Array.Empty<FollowerInventoryItemViewDto>();
        }

        var itemsByParent = owner.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var itemsById = owner.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (!itemsById.TryGetValue(owner.RootId, out var rootItem))
        {
            return Array.Empty<FollowerInventoryItemViewDto>();
        }

        var collected = new List<FollowerInventoryItemViewDto> { rootItem };
        var seen = new HashSet<string>(StringComparer.Ordinal) { rootItem.Id };
        var queue = new Queue<string>();
        queue.Enqueue(rootItem.Id);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!itemsByParent.TryGetValue(currentId, out var children))
            {
                continue;
            }

            foreach (var child in children.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (!seen.Add(child.Id))
                {
                    continue;
                }

                collected.Add(child);
                queue.Enqueue(child.Id);
            }
        }

        return collected;
    }
}

using System.Globalization;
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerPlayerInventoryLiveRefreshPolicy
{
    public static IReadOnlyList<FollowerInventoryItemViewDto> NormalizeForLiveRefresh(FollowerInventoryOwnerViewDto? owner)
    {
        if (owner is null || owner.Items.Count == 0)
        {
            return Array.Empty<FollowerInventoryItemViewDto>();
        }

        var normalized = owner.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        var indexedGroups = normalized
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId) && !string.IsNullOrWhiteSpace(item.SlotId))
            .GroupBy(item => $"{item.ParentId}\n{item.SlotId}", StringComparer.Ordinal);
        foreach (var group in indexedGroups)
        {
            var entries = group
                .Select((item, order) => new
                {
                    Item = item,
                    Order = order,
                    Index = TryReadIndexedLocation(item.LocationJson),
                    HasStructuredLocation = HasStructuredLocation(item.LocationJson),
                })
                .ToArray();
            if (entries.Length < 2 || entries.Any(entry => entry.HasStructuredLocation))
            {
                continue;
            }

            var orderedEntries = entries
                .OrderBy(entry => entry.Index ?? int.MaxValue)
                .ThenBy(entry => entry.Order)
                .ToArray();
            for (var index = 0; index < orderedEntries.Length; index++)
            {
                var current = orderedEntries[index].Item;
                normalized[normalized.FindIndex(item => string.Equals(item.Id, current.Id, StringComparison.Ordinal))] =
                    current with
                    {
                        LocationJson = index.ToString(CultureInfo.InvariantCulture),
                    };
            }
        }

        return normalized;
    }

    private static int? TryReadIndexedLocation(string? locationJson)
    {
        return int.TryParse(locationJson, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static bool HasStructuredLocation(string? locationJson)
    {
        if (string.IsNullOrWhiteSpace(locationJson))
        {
            return false;
        }

        var trimmed = locationJson.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }
}

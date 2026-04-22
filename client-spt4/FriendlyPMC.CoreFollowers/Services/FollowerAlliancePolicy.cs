namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerAlliancePolicy
{
    public static bool? GetPlayerEnemyOverride(
        string? initialBotProfileId,
        IEnumerable<string> groupMemberProfileIds,
        IEnumerable<string> registeredFollowerProfileIds,
        string? ownerProfileId,
        string? targetProfileId)
    {
        if (string.IsNullOrWhiteSpace(targetProfileId))
        {
            return null;
        }

        var registeredFollowers = registeredFollowerProfileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (registeredFollowers.Count == 0)
        {
            return null;
        }

        var isFollowerGroup = (!string.IsNullOrWhiteSpace(initialBotProfileId) && registeredFollowers.Contains(initialBotProfileId))
            || groupMemberProfileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Any(registeredFollowers.Contains);
        if (!isFollowerGroup)
        {
            return null;
        }

        if (string.Equals(targetProfileId, ownerProfileId, StringComparison.Ordinal))
        {
            return false;
        }

        if (registeredFollowers.Contains(targetProfileId))
        {
            return false;
        }

        return null;
    }
}

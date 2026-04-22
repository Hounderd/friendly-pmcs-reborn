namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerProtectionPolicy
{
    public static bool ShouldProtectPlayer(
        string? botProfileId,
        IEnumerable<string> registeredFollowerProfileIds,
        string? ownerProfileId,
        string? targetProfileId)
    {
        if (string.IsNullOrWhiteSpace(botProfileId) || string.IsNullOrWhiteSpace(targetProfileId))
        {
            return false;
        }

        var registeredFollowers = registeredFollowerProfileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (!registeredFollowers.Contains(botProfileId))
        {
            return false;
        }

        return string.Equals(targetProfileId, ownerProfileId, StringComparison.Ordinal)
            || registeredFollowers.Contains(targetProfileId);
    }
}

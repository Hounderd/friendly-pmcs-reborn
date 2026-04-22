namespace FriendlyPMC.CoreFollowers.Services;

public static class DebugFollowerBossSetupPolicy
{
    public static bool ShouldSkipNativeBossSetup(
        string? ownerProfileId,
        IEnumerable<string> registeredFollowerProfileIds)
    {
        if (string.IsNullOrWhiteSpace(ownerProfileId))
        {
            return false;
        }

        return registeredFollowerProfileIds.Any(id => string.Equals(id, ownerProfileId, StringComparison.Ordinal));
    }
}

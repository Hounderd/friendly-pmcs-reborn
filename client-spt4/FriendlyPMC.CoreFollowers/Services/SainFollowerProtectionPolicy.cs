namespace FriendlyPMC.CoreFollowers.Services;

public static class SainFollowerProtectionPolicy
{
    public static bool ShouldSuppressProtectedEnemy(
        string? botProfileId,
        IEnumerable<string> registeredFollowerProfileIds,
        string? ownerProfileId,
        string? targetProfileId)
    {
        return FollowerProtectionPolicy.ShouldProtectPlayer(
            botProfileId,
            registeredFollowerProfileIds,
            ownerProfileId,
            targetProfileId);
    }
}

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerIdentityResolver
{
    public static string Resolve(string? profileId, string? accountId)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            return profileId;
        }

        return accountId ?? string.Empty;
    }
}

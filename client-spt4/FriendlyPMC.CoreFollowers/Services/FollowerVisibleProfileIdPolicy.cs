namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerVisibleProfileIdPolicy
{
    public static string Normalize(
        string? accountId,
        IEnumerable<FollowerInventoryFriendReference>? friends)
    {
        var normalizedAccountId = accountId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedAccountId))
        {
            return string.Empty;
        }

        var decision = FollowerInventoryEntryPolicy.CreateDecision(
            normalizedAccountId,
            null,
            friends);

        return decision.ShouldShowInventoryAction && !string.IsNullOrWhiteSpace(decision.FollowerAid)
            ? decision.FollowerAid
            : normalizedAccountId;
    }
}

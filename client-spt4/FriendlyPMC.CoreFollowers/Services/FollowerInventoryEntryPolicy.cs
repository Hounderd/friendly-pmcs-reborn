namespace FriendlyPMC.CoreFollowers.Services;

internal readonly record struct FollowerInventoryFriendReference(
    string Id,
    string AccountId,
    string Nickname);

internal readonly record struct FollowerInventoryEntryDecision(
    bool ShouldShowInventoryAction,
    string FollowerAid,
    string Nickname);

internal static class FollowerInventoryEntryPolicy
{
    private const string SquadManagerId = "67b0f29e151899410b04aacb";
    private const string SquadManagerNickname = "Squad Manager";

    public static FollowerInventoryEntryDecision CreateDecision(
        string? viewedAccountId,
        string? viewedNickname,
        IEnumerable<FollowerInventoryFriendReference>? friends)
    {
        var normalizedViewedAccountId = viewedAccountId?.Trim() ?? string.Empty;
        var normalizedViewedNickname = viewedNickname?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedViewedAccountId))
        {
            return default;
        }

        if (string.Equals(normalizedViewedAccountId, SquadManagerId, StringComparison.Ordinal)
            || string.Equals(normalizedViewedNickname, SquadManagerNickname, StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        var friendCandidates = friends?.ToArray() ?? Array.Empty<FollowerInventoryFriendReference>();
        var followerFriend = friendCandidates
            .FirstOrDefault(friend =>
                string.Equals(friend.Id, normalizedViewedAccountId, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(followerFriend.Id))
        {
            if (string.IsNullOrWhiteSpace(followerFriend.AccountId))
            {
                return default;
            }

            return new FollowerInventoryEntryDecision(
                true,
                followerFriend.Id,
                string.IsNullOrWhiteSpace(followerFriend.Nickname)
                    ? normalizedViewedNickname
                    : followerFriend.Nickname);
        }

        var socialAidMatches = friendCandidates
            .Where(friend => string.Equals(friend.AccountId, normalizedViewedAccountId, StringComparison.Ordinal))
            .ToArray();
        if (socialAidMatches.Length != 1)
        {
            return default;
        }

        followerFriend = socialAidMatches[0];
        if (string.IsNullOrWhiteSpace(followerFriend.Id)
            || string.Equals(followerFriend.Id, normalizedViewedAccountId, StringComparison.Ordinal))
        {
            return default;
        }

        return new FollowerInventoryEntryDecision(
            true,
            followerFriend.Id,
            string.IsNullOrWhiteSpace(followerFriend.Nickname)
                ? normalizedViewedNickname
                : followerFriend.Nickname);
    }
}

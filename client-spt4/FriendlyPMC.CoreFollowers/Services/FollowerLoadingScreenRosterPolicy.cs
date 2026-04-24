using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerLoadingScreenRosterPolicy
{
    public static IReadOnlyList<string> BuildLines(IEnumerable<FollowerSnapshotDto> followers)
    {
        return followers
            .Where(follower => !string.IsNullOrWhiteSpace(follower.Nickname))
            .Select(FormatLine)
            .ToArray();
    }

    private static string FormatLine(FollowerSnapshotDto follower)
    {
        var side = string.IsNullOrWhiteSpace(follower.Side)
            ? "PMC"
            : follower.Side.Trim().ToUpperInvariant();

        return $"    {side} {follower.Nickname.Trim()}";
    }
}

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

        return $"<color=#8f9b8e>+ {side}</color> <color=#d2d2c8>{follower.Nickname.Trim()}</color>";
    }
}

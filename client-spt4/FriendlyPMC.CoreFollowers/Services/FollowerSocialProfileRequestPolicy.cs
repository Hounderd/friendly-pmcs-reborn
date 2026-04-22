using System.Text.RegularExpressions;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerSocialProfileRequestPolicy
{
    private const string SquadManagerId = "67b0f29e151899410b04aacb";
    private static readonly Regex MongoIdPattern = new("^[0-9a-fA-F]{24}$", RegexOptions.Compiled);

    public static string? TryRemapRequestedAccountId(
        string? requestedAccountId,
        string? currentPlayerAccountId,
        string? selectedProfileId,
        string? selectedProfileAccountId)
    {
        if (string.IsNullOrWhiteSpace(selectedProfileId)
            || !MongoIdPattern.IsMatch(selectedProfileId)
            || string.Equals(selectedProfileId, SquadManagerId, StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(requestedAccountId, selectedProfileId, StringComparison.Ordinal))
        {
            return null;
        }

        var normalizedSelectedAccountId = selectedProfileAccountId?.Trim();
        var hasUsableSelectedAccountId =
            !string.IsNullOrWhiteSpace(normalizedSelectedAccountId)
            && !string.Equals(normalizedSelectedAccountId, "0", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(requestedAccountId) || !hasUsableSelectedAccountId)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(currentPlayerAccountId)
            && string.Equals(requestedAccountId, currentPlayerAccountId, StringComparison.Ordinal))
        {
            return selectedProfileId;
        }

        if (!string.Equals(requestedAccountId, normalizedSelectedAccountId, StringComparison.Ordinal))
        {
            return null;
        }

        return selectedProfileId;
    }
}

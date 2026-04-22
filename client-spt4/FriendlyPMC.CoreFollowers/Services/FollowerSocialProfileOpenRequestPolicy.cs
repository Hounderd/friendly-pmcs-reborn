namespace FriendlyPMC.CoreFollowers.Services;

internal readonly record struct FollowerSocialProfileOpenPlan(
    string RequestedAccountId,
    string ResolvedAccountId,
    bool ShouldHandleDirectly,
    bool ShouldTemporarilyRewriteSelectedAccountId);

internal static class FollowerSocialProfileOpenRequestPolicy
{
    private const string FriendContext = "friend-context";

    public static string? ResolveRequestedAccountId(
        string? requestedAccountId,
        string? selectedProfileId,
        string? selectedProfileAccountId)
    {
        var normalizedRequestedAccountId = requestedAccountId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRequestedAccountId))
        {
            return null;
        }

        var remappedAccountId = FollowerSocialProfileRequestPolicy.TryRemapRequestedAccountId(
            normalizedRequestedAccountId,
            null,
            selectedProfileId,
            selectedProfileAccountId);

        return string.IsNullOrWhiteSpace(remappedAccountId)
            ? normalizedRequestedAccountId
            : remappedAccountId;
    }

    public static FollowerSocialProfileOpenPlan CreatePlan(
        string source,
        string? requestedAccountId,
        string? selectedProfileId,
        string? selectedProfileAccountId)
    {
        var normalizedRequestedAccountId = requestedAccountId?.Trim() ?? string.Empty;
        var resolvedAccountId = ResolveRequestedAccountId(
            normalizedRequestedAccountId,
            selectedProfileId,
            selectedProfileAccountId) ?? string.Empty;

        var remapped =
            !string.IsNullOrWhiteSpace(normalizedRequestedAccountId)
            && !string.IsNullOrWhiteSpace(resolvedAccountId)
            && !string.Equals(normalizedRequestedAccountId, resolvedAccountId, StringComparison.Ordinal);

        var preserveOriginalFriendInteraction = string.Equals(source, FriendContext, StringComparison.Ordinal);
        return new FollowerSocialProfileOpenPlan(
            normalizedRequestedAccountId,
            string.IsNullOrWhiteSpace(resolvedAccountId) ? normalizedRequestedAccountId : resolvedAccountId,
            ShouldHandleDirectly: !preserveOriginalFriendInteraction,
            ShouldTemporarilyRewriteSelectedAccountId: preserveOriginalFriendInteraction && remapped);
    }
}

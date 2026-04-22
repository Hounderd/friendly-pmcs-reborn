namespace FriendlyPMC.CoreFollowers.Services;

internal readonly record struct FollowerSocialStartupSmokeCandidate(
    string Id,
    string AccountId,
    string Nickname);

internal static class FollowerSocialStartupSmokePolicy
{
    private const string FriendContext = "friend-context";

    public static FollowerSocialStartupSmokeCandidate? TrySelectCandidate(IEnumerable<FollowerSocialStartupSmokeCandidate> candidates)
    {
        var candidateArray = candidates.ToArray();
        var uniqueAccountIds = candidateArray
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.AccountId))
            .GroupBy(candidate => candidate.AccountId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in candidateArray)
        {
            if (string.IsNullOrWhiteSpace(candidate.Id) || string.IsNullOrWhiteSpace(candidate.AccountId))
            {
                continue;
            }

            var plan = FollowerSocialProfileOpenRequestPolicy.CreatePlan(
                FriendContext,
                candidate.AccountId,
                candidate.Id,
                candidate.AccountId);

            if ((plan.ShouldTemporarilyRewriteSelectedAccountId || plan.ShouldHandleDirectly)
                && uniqueAccountIds.Contains(candidate.AccountId))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string ResolveInvocationAccountId(FollowerSocialStartupSmokeCandidate candidate)
    {
        var plan = FollowerSocialProfileOpenRequestPolicy.CreatePlan(
            FriendContext,
            candidate.AccountId,
            candidate.Id,
            candidate.AccountId);

        return string.IsNullOrWhiteSpace(plan.ResolvedAccountId)
            ? candidate.AccountId
            : plan.ResolvedAccountId;
    }
}

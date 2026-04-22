namespace FriendlyPMC.CoreFollowers.Services;

public static class BotDebugSelectionPolicy
{
    public static IReadOnlyList<BotDebugSelectionCandidate> SelectComparisonBots(
        IEnumerable<BotDebugSelectionCandidate> candidates,
        int maxCount,
        float maxDistanceToPlayer,
        float maxDistanceToFollower)
    {
        return candidates
            .Where(candidate =>
                candidate.DistanceToPlayer <= maxDistanceToPlayer
                || candidate.DistanceToNearestFollower <= maxDistanceToFollower)
            .OrderBy(candidate => candidate.SortDistance)
            .ThenBy(candidate => candidate.ProfileId, StringComparer.Ordinal)
            .Take(maxCount)
            .ToArray();
    }
}

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerPlayerShotBootstrapCandidate(
    string ProfileId,
    float AngleDegrees,
    float DistanceMeters);

public static class FollowerPlayerShotBootstrapPolicy
{
    public const float DefaultMaxBootstrapDistanceMeters = 45f;
    public const float DefaultAimConeDegrees = 18f;
    public const float DefaultCloseRangeFallbackDistanceMeters = 16f;

    public static string? ResolveTargetProfileId(
        bool playerHasRecentShot,
        IEnumerable<FollowerPlayerShotBootstrapCandidate> candidates,
        float maxBootstrapDistanceMeters = DefaultMaxBootstrapDistanceMeters,
        float maxAimConeDegrees = DefaultAimConeDegrees,
        float closeRangeFallbackDistanceMeters = DefaultCloseRangeFallbackDistanceMeters)
    {
        if (!playerHasRecentShot)
        {
            return null;
        }

        var candidateArray = candidates
            .Where(candidate =>
                !string.IsNullOrWhiteSpace(candidate.ProfileId)
                && candidate.DistanceMeters >= 0f
                && candidate.DistanceMeters <= maxBootstrapDistanceMeters)
            .ToArray();
        if (candidateArray.Length == 0)
        {
            return null;
        }

        return candidateArray
            .Where(candidate => candidate.AngleDegrees <= maxAimConeDegrees)
            .OrderBy(candidate => candidate.AngleDegrees)
            .ThenBy(candidate => candidate.DistanceMeters)
            .Select(candidate => candidate.ProfileId)
            .FirstOrDefault()
            ?? candidateArray
                .Where(candidate => candidate.DistanceMeters <= closeRangeFallbackDistanceMeters)
                .OrderBy(candidate => candidate.DistanceMeters)
                .ThenBy(candidate => candidate.AngleDegrees)
                .Select(candidate => candidate.ProfileId)
                .FirstOrDefault();
    }
}

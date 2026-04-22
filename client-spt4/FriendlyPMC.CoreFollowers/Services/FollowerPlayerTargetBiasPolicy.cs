namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerTargetBiasCandidate(
    string ProfileId,
    float AngleDegrees,
    bool IsVisible,
    bool CanShoot);

public readonly record struct FollowerCurrentTargetState(
    string? ProfileId,
    bool IsVisible,
    bool CanShoot,
    bool IsProtected);

public static class FollowerPlayerTargetBiasPolicy
{
    public const float DefaultAimConeDegrees = 15f;

    public static string? ResolveAssistTargetProfileId(
        bool playerIsActivelyEngaged,
        FollowerCurrentTargetState currentTarget,
        string? directTargetProfileId,
        IEnumerable<FollowerTargetBiasCandidate> candidates,
        float maxAimConeDegrees = DefaultAimConeDegrees)
    {
        if (HasHealthyCurrentTarget(currentTarget))
        {
            return currentTarget.ProfileId;
        }

        var preferredTargetProfileId = ResolvePreferredTargetProfileId(
                playerIsActivelyEngaged,
                directTargetProfileId)
            ?? ResolvePreferredKnownEnemyProfileId(
                playerIsActivelyEngaged,
                candidates,
                maxAimConeDegrees);

        if (!string.IsNullOrWhiteSpace(preferredTargetProfileId))
        {
            return preferredTargetProfileId;
        }

        return currentTarget.IsProtected
            ? null
            : currentTarget.ProfileId;
    }

    public static string? ResolvePreferredTargetProfileId(
        bool playerIsActivelyEngaged,
        string? directTargetProfileId,
        string? fallbackTargetProfileId = null)
    {
        if (!playerIsActivelyEngaged)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(directTargetProfileId)
            ? directTargetProfileId
            : fallbackTargetProfileId;
    }

    public static string? ResolvePreferredKnownEnemyProfileId(
        bool playerIsActivelyEngaged,
        IEnumerable<FollowerTargetBiasCandidate> candidates,
        float maxAimConeDegrees = DefaultAimConeDegrees)
    {
        if (!playerIsActivelyEngaged)
        {
            return null;
        }

        var candidateArray = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.ProfileId))
            .ToArray();

        return candidateArray
            .Where(candidate =>
                candidate.AngleDegrees <= maxAimConeDegrees)
            .OrderByDescending(candidate => candidate.IsVisible)
            .ThenByDescending(candidate => candidate.CanShoot)
            .ThenBy(candidate => candidate.AngleDegrees)
            .Select(candidate => candidate.ProfileId)
            .FirstOrDefault()
            ?? candidateArray
                .OrderByDescending(candidate => candidate.IsVisible)
                .ThenByDescending(candidate => candidate.CanShoot)
                .ThenBy(candidate => candidate.AngleDegrees)
                .Select(candidate => candidate.ProfileId)
                .FirstOrDefault();
    }

    private static bool HasHealthyCurrentTarget(FollowerCurrentTargetState currentTarget)
    {
        return !currentTarget.IsProtected
            && !string.IsNullOrWhiteSpace(currentTarget.ProfileId)
            && (currentTarget.IsVisible || currentTarget.CanShoot);
    }
}

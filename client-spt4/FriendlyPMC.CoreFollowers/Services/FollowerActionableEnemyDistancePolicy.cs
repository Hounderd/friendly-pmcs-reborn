namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerActionableEnemyDistanceCandidate(
    bool IsActionable,
    float DistanceMeters);

public static class FollowerActionableEnemyDistancePolicy
{
    public static float ResolveNearestDistance(
        FollowerActionableEnemyDistanceCandidate goalEnemy,
        IEnumerable<FollowerActionableEnemyDistanceCandidate> knownEnemies)
    {
        var nearestDistance = ResolveCandidateDistance(goalEnemy);
        foreach (var enemy in knownEnemies)
        {
            var candidateDistance = ResolveCandidateDistance(enemy);
            if (candidateDistance < nearestDistance)
            {
                nearestDistance = candidateDistance;
            }
        }

        return nearestDistance;
    }

    private static float ResolveCandidateDistance(FollowerActionableEnemyDistanceCandidate candidate)
    {
        if (!candidate.IsActionable
            || float.IsNaN(candidate.DistanceMeters)
            || float.IsInfinity(candidate.DistanceMeters)
            || candidate.DistanceMeters < 0f)
        {
            return float.MaxValue;
        }

        return candidate.DistanceMeters;
    }
}

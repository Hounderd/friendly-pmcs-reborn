namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerMovementProgressSample(float timeSeconds, float distanceToTargetMeters)
{
    public float TimeSeconds { get; } = timeSeconds;

    public float DistanceToTargetMeters { get; } = distanceToTargetMeters;
}

public static class FollowerUnstuckPolicy
{
    private const float DefaultRefreshTimeoutSeconds = 2f;
    private const float DefaultMinimumProgressMeters = 0.5f;

    public static bool ShouldRefreshMovement(
        bool movementRequested,
        FollowerMovementProgressSample previous,
        FollowerMovementProgressSample current,
        float refreshTimeoutSeconds = DefaultRefreshTimeoutSeconds,
        float minimumProgressMeters = DefaultMinimumProgressMeters)
    {
        if (!movementRequested)
        {
            return false;
        }

        var elapsed = current.TimeSeconds - previous.TimeSeconds;
        if (elapsed < refreshTimeoutSeconds)
        {
            return false;
        }

        var progress = previous.DistanceToTargetMeters - current.DistanceToTargetMeters;
        return progress < minimumProgressMeters;
    }
}

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerEnvironmentClassifierPolicy
{
    public const float IndoorCeilingProbeMeters = 7f;

    public static FollowerFormationSpacingProfile Resolve(
        bool hasOverheadHit,
        float overheadHitDistanceMeters)
    {
        return hasOverheadHit && overheadHitDistanceMeters <= IndoorCeilingProbeMeters
            ? FollowerFormationSpacingProfile.Indoor
            : FollowerFormationSpacingProfile.Outdoor;
    }
}

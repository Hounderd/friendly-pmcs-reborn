namespace FriendlyPMC.CoreFollowers.Services;

public sealed class FollowerModeSettings
{
    public const float DefaultFollowLeashDistanceMeters = 20f;
    public const float DefaultHoldRadiusMeters = 20f;
    public const float DefaultFollowDeadzoneMeters = 2f;
    public const float DefaultCatchUpDistanceMeters = 15f;
    public const float DefaultCombatMaxRangeMeters = 100f;

    public FollowerModeSettings(
        float followLeashDistanceMeters = DefaultFollowLeashDistanceMeters,
        float holdRadiusMeters = DefaultHoldRadiusMeters,
        float followDeadzoneMeters = DefaultFollowDeadzoneMeters,
        float catchUpDistanceMeters = DefaultCatchUpDistanceMeters,
        float combatMaxRangeMeters = DefaultCombatMaxRangeMeters)
    {
        FollowLeashDistanceMeters = NormalizeDistance(
            followLeashDistanceMeters,
            DefaultFollowLeashDistanceMeters);
        HoldRadiusMeters = NormalizeDistance(
            holdRadiusMeters,
            DefaultHoldRadiusMeters);
        FollowDeadzoneMeters = NormalizeDistance(
            followDeadzoneMeters,
            DefaultFollowDeadzoneMeters);
        CatchUpDistanceMeters = NormalizeDistance(
            catchUpDistanceMeters,
            DefaultCatchUpDistanceMeters);
        CombatMaxRangeMeters = NormalizeDistance(
            combatMaxRangeMeters,
            DefaultCombatMaxRangeMeters);
    }

    public float FollowLeashDistanceMeters { get; }

    public float HoldRadiusMeters { get; }

    public float FollowDeadzoneMeters { get; }

    public float CatchUpDistanceMeters { get; }

    public float CombatMaxRangeMeters { get; }

    public float EffectiveCatchUpDistanceMeters =>
        MathF.Max(FollowDeadzoneMeters, CatchUpDistanceMeters);

    private static float NormalizeDistance(float value, float fallback)
    {
        return value > 0f ? value : fallback;
    }
}

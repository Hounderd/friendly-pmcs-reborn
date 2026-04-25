namespace FriendlyPMC.CoreFollowers.Services;

public static class SainFollowerAccuracyPolicy
{
    private const float FollowerScatteringCoef = 0.35f;
    private const float FollowerAccuracySpeedCoef = 0.45f;
    private const float FollowerPrecisionSpeedCoef = 1.5f;
    private const float FollowerVisibleDistanceCoef = 1.35f;
    private const float FollowerHearingDistanceCoef = 1.3f;
    private const float FollowerSainDifficultyModifier = 1.75f;

    public static float ResolveScatteringCoef(float currentValue)
    {
        return MathF.Min(currentValue, FollowerScatteringCoef);
    }

    public static float ResolveAccuracySpeedCoef(float currentValue)
    {
        return MathF.Min(currentValue, FollowerAccuracySpeedCoef);
    }

    public static float ResolvePrecisionSpeedCoef(float currentValue)
    {
        return MathF.Max(currentValue, FollowerPrecisionSpeedCoef);
    }

    public static float ResolveVisibleDistanceCoef(float currentValue)
    {
        return MathF.Max(currentValue, FollowerVisibleDistanceCoef);
    }

    public static float ResolveHearingDistanceCoef(float currentValue)
    {
        return MathF.Max(currentValue, FollowerHearingDistanceCoef);
    }

    public static float ResolveSainDifficultyModifier(float currentValue)
    {
        return MathF.Max(currentValue, FollowerSainDifficultyModifier);
    }
}

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerPlateHealthBarPolicy
{
    public static float ResolveFillRatio(int healthPercent)
    {
        return Math.Clamp(healthPercent / 100f, 0f, 1f);
    }

    public static float ResolveFillWidth(float maxFillWidth, int healthPercent)
    {
        return maxFillWidth * ResolveFillRatio(healthPercent);
    }
}

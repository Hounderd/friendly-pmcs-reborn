namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerMainMenuRosterLayout(
    float AnchorMinX,
    float AnchorMinY,
    float AnchorMaxX,
    float AnchorMaxY,
    float PivotX,
    float PivotY,
    float PositionX,
    float PositionY);

public readonly record struct FollowerMainMenuRosterPosition(float X, float Y);

public static class FollowerMainMenuRosterLayoutPolicy
{
    private const float LoadingRosterFontSize = 22f;
    private const float VersionTextVerticalOffset = 32f;

    public static FollowerMainMenuRosterLayout Resolve()
    {
        return new FollowerMainMenuRosterLayout(
            AnchorMinX: 0f,
            AnchorMinY: 0f,
            AnchorMaxX: 0f,
            AnchorMaxY: 0f,
            PivotX: 0f,
            PivotY: 0f,
            PositionX: 0f,
            PositionY: VersionTextVerticalOffset);
    }

    public static float ResolveFollowerFontSize(float templateFontSize)
    {
        return MathF.Min(templateFontSize, LoadingRosterFontSize);
    }

    public static bool ShouldInject(bool hasReliableFooterAnchor)
    {
        return hasReliableFooterAnchor;
    }

    public static FollowerMainMenuRosterPosition ResolvePositionFromVersionText(float versionTextX, float versionTextY)
    {
        return new FollowerMainMenuRosterPosition(
            versionTextX,
            versionTextY + VersionTextVerticalOffset);
    }
}

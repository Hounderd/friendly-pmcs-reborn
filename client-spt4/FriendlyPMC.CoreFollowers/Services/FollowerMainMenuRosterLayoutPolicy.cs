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

public static class FollowerMainMenuRosterLayoutPolicy
{
    private const float LoadingRosterFontSize = 22f;

    public static FollowerMainMenuRosterLayout Resolve()
    {
        return new FollowerMainMenuRosterLayout(
            AnchorMinX: 0f,
            AnchorMinY: 0f,
            AnchorMaxX: 0f,
            AnchorMaxY: 0f,
            PivotX: 0f,
            PivotY: 0f,
            PositionX: 108f,
            PositionY: 66f);
    }

    public static float ResolveFollowerFontSize(float templateFontSize)
    {
        return MathF.Min(templateFontSize, LoadingRosterFontSize);
    }
}

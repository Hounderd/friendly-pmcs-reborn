namespace FriendlyPMC.CoreFollowers.Services;

public sealed record FollowerPlateSettings
{
    public const float DefaultScale = 1f;
    public const float DefaultMaxDistanceMeters = 500f;
    public const float DefaultVerticalOffsetWorld = 0.35f;

    public FollowerPlateSettings(
        bool enabled = true,
        float scale = DefaultScale,
        float maxDistanceMeters = DefaultMaxDistanceMeters,
        bool showHealthBar = true,
        bool showHealthNumber = false,
        bool showFactionBadge = true,
        float verticalOffsetWorld = DefaultVerticalOffsetWorld)
    {
        Enabled = enabled;
        Scale = scale > 0f ? scale : DefaultScale;
        MaxDistanceMeters = maxDistanceMeters > 0f ? maxDistanceMeters : DefaultMaxDistanceMeters;
        ShowHealthBar = showHealthBar;
        ShowHealthNumber = showHealthNumber;
        ShowFactionBadge = showFactionBadge;
        VerticalOffsetWorld = verticalOffsetWorld;
    }

    public bool Enabled { get; }

    public float Scale { get; }

    public float MaxDistanceMeters { get; }

    public bool ShowHealthBar { get; }

    public bool ShowHealthNumber { get; }

    public bool ShowFactionBadge { get; }

    public float VerticalOffsetWorld { get; }
}

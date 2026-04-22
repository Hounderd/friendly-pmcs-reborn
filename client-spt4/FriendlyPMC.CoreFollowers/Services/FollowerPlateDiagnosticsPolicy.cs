namespace FriendlyPMC.CoreFollowers.Services;

public enum FollowerPlateHiddenReason
{
    None = 0,
    SettingsDisabled = 1,
    NotOperational = 2,
    OutOfRange = 3,
    ProjectionFailed = 4,
}

public readonly record struct FollowerPlateDiagnosticState(bool IsVisible, FollowerPlateHiddenReason HiddenReason);

public static class FollowerPlateDiagnosticsPolicy
{
    public static FollowerPlateHiddenReason ResolveHiddenReason(
        bool isEnabled,
        bool isOperational,
        float distanceToPlayerMeters,
        float maxDistanceMeters,
        bool projectionFailed)
    {
        if (!isEnabled)
        {
            return FollowerPlateHiddenReason.SettingsDisabled;
        }

        if (!isOperational)
        {
            return FollowerPlateHiddenReason.NotOperational;
        }

        if (distanceToPlayerMeters > maxDistanceMeters)
        {
            return FollowerPlateHiddenReason.OutOfRange;
        }

        return projectionFailed
            ? FollowerPlateHiddenReason.ProjectionFailed
            : FollowerPlateHiddenReason.None;
    }

    public static string? BuildTransitionMessage(
        string nickname,
        string aid,
        FollowerPlateDiagnosticState? previous,
        FollowerPlateDiagnosticState current)
    {
        if (previous.HasValue && previous.Value.Equals(current))
        {
            return null;
        }

        if (!previous.HasValue)
        {
            return current.IsVisible
                ? $"Follower plate created visible: follower={nickname}, aid={aid}"
                : $"Follower plate created hidden: follower={nickname}, aid={aid}, reason={current.HiddenReason}";
        }

        return current.IsVisible
            ? $"Follower plate shown: follower={nickname}, aid={aid}"
            : $"Follower plate hidden: follower={nickname}, aid={aid}, reason={current.HiddenReason}";
    }

    public static string BuildSummaryMessage(int runtimeFollowerCount, int visiblePlateCount, int hiddenPlateCount)
    {
        return $"Follower plate summary: runtimeFollowers={runtimeFollowerCount}, visible={visiblePlateCount}, hidden={hiddenPlateCount}";
    }

    public static string BuildEligibilityMessage(string nickname, string aid, string side, int healthPercent)
    {
        return $"Follower plate eligible: follower={nickname}, aid={aid}, side={side}, health={healthPercent}%";
    }
}

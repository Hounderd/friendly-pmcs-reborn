namespace FriendlyPMC.CoreFollowers.Services;

public enum WaypointsDependencyOutcome
{
    UseCustomBrain = 0,
    UseFallback = 1,
    Abort = 2,
}

public static class WaypointsDependencyPolicy
{
    public static WaypointsDependencyOutcome Resolve(
        bool useCustomBrain,
        bool fallbackToLegacyPath,
        bool isWaypointsAvailable)
    {
        if (!useCustomBrain)
        {
            return WaypointsDependencyOutcome.UseFallback;
        }

        if (isWaypointsAvailable)
        {
            return WaypointsDependencyOutcome.UseCustomBrain;
        }

        return fallbackToLegacyPath
            ? WaypointsDependencyOutcome.UseFallback
            : WaypointsDependencyOutcome.Abort;
    }
}

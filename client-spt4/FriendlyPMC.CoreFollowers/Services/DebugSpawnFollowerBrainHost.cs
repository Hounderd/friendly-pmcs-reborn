namespace FriendlyPMC.CoreFollowers.Services;

public static class DebugSpawnFollowerBrainHost
{
    public static DebugSpawnFollowerControlDecision ResolveControlPath(
        bool useCustomBrain,
        bool fallbackToLegacyPath,
        bool isWaypointsAvailable)
    {
        var dependencyOutcome = WaypointsDependencyPolicy.Resolve(
            useCustomBrain,
            fallbackToLegacyPath,
            isWaypointsAvailable);

        return dependencyOutcome switch
        {
            WaypointsDependencyOutcome.UseCustomBrain => new DebugSpawnFollowerControlDecision(
                DebugSpawnFollowerControlPath.CustomBrain),
            WaypointsDependencyOutcome.UseFallback => new DebugSpawnFollowerControlDecision(
                DebugSpawnFollowerControlPath.LegacyFallback),
            WaypointsDependencyOutcome.Abort => new DebugSpawnFollowerControlDecision(
                DebugSpawnFollowerControlPath.Abort,
                "WaypointsRequired"),
            _ => throw new ArgumentOutOfRangeException(nameof(dependencyOutcome), dependencyOutcome, null),
        };
    }
}

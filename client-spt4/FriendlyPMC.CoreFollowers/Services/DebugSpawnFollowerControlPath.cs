namespace FriendlyPMC.CoreFollowers.Services;

public enum DebugSpawnFollowerControlPath
{
    CustomBrain = 0,
    LegacyFallback = 1,
    Abort = 2,
}

public readonly record struct DebugSpawnFollowerControlDecision(
    DebugSpawnFollowerControlPath ControlPath,
    string? AbortReason = null);

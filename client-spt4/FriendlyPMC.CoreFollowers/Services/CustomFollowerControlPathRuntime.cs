namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerControlPathRuntime(
    DebugSpawnFollowerControlPath ActivePath,
    bool LegacyPathSuppressed,
    string? AbortReason = null)
{
    public static CustomFollowerControlPathRuntime Create(
        DebugSpawnFollowerControlPath activePath,
        string? abortReason)
    {
        return activePath switch
        {
            DebugSpawnFollowerControlPath.CustomBrain => new CustomFollowerControlPathRuntime(
                activePath,
                LegacyPathSuppressed: true,
                AbortReason: null),
            DebugSpawnFollowerControlPath.LegacyFallback => new CustomFollowerControlPathRuntime(
                activePath,
                LegacyPathSuppressed: false,
                AbortReason: null),
            DebugSpawnFollowerControlPath.Abort => new CustomFollowerControlPathRuntime(
                activePath,
                LegacyPathSuppressed: false,
                AbortReason: abortReason),
            _ => throw new ArgumentOutOfRangeException(nameof(activePath), activePath, null),
        };
    }
}

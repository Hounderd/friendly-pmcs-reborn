namespace FriendlyPMC.CoreFollowers.Services;

public static class DebugSpawnFollowerLegacySeedPolicy
{
    public static bool ShouldSeedLegacyFollowOrder(DebugSpawnFollowerControlPath controlPath)
    {
        return controlPath != DebugSpawnFollowerControlPath.CustomBrain;
    }
}

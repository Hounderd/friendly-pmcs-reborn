using FriendlyPMC.CoreFollowers.Modules;

namespace FriendlyPMC.CoreFollowers.Services;

public static class DebugSpawnFollowerSessionBinder
{
    public static bool Bind(FollowerRegistry registry, string aid, DebugSpawnFollowerControlPath controlPath)
    {
        if (controlPath != DebugSpawnFollowerControlPath.CustomBrain)
        {
            return false;
        }

        registry.SetCustomBrainSession(aid, new CustomFollowerBrainRuntimeSession());
        return true;
    }
}

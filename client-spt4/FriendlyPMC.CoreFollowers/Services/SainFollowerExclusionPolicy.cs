using FriendlyPMC.CoreFollowers.Modules;
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class SainFollowerExclusionPolicy
{
    public static bool ShouldExcludeFollower(FollowerRegistry? registry, string? profileId)
    {
        if (registry is null || string.IsNullOrWhiteSpace(profileId))
        {
            return false;
        }

        if (!registry.IsManagedRuntimeProfileId(profileId))
        {
            return false;
        }

        if (ShouldAllowSainCombat(registry, profileId))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldAllowSainCombat(FollowerRegistry registry, string profileId)
    {
        if (registry.TryGetCustomBrainSessionByProfileId(profileId, out var customBrainSession))
        {
            return customBrainSession.CurrentDebugState.Mode is
                CustomFollowerBrainMode.CombatPursue or
                CustomFollowerBrainMode.CombatReturnToRange;
        }

        return registry.TryGetActiveOrderByProfileId(profileId, out var activeOrder)
            && activeOrder is FollowerCommand.Combat or FollowerCommand.TakeCover;
    }
}

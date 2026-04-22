namespace FriendlyPMC.CoreFollowers.Services;

public static class FriendlyFollowerMovementLayerActivationPolicy
{
    public static bool ShouldActivate(
        DebugSpawnFollowerControlPath? controlPath,
        CustomFollowerBrainMode? customBrainMode)
    {
        if (controlPath != DebugSpawnFollowerControlPath.CustomBrain)
        {
            return true;
        }

        return customBrainMode is CustomFollowerBrainMode.FollowFormation
            or CustomFollowerBrainMode.FollowCatchUp
            or CustomFollowerBrainMode.RecoverNavigation
            or CustomFollowerBrainMode.CombatReturnToRange;
    }
}

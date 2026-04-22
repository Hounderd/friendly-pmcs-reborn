namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerMaintenanceDirective(
    bool SuppressLegacyMaintenance,
    CustomFollowerNavigationIntent? NavigationIntent,
    bool ShouldRefreshHold);

public static class CustomFollowerMaintenancePolicy
{
    public static CustomFollowerMaintenanceDirective Resolve(CustomFollowerBrainTickResult tickResult)
    {
        return tickResult.DebugState.Mode switch
        {
            CustomFollowerBrainMode.FollowFormation => new CustomFollowerMaintenanceDirective(
                SuppressLegacyMaintenance: true,
                NavigationIntent: CustomFollowerNavigationIntent.MoveToFormation,
                ShouldRefreshHold: false),
            CustomFollowerBrainMode.FollowCatchUp => new CustomFollowerMaintenanceDirective(
                SuppressLegacyMaintenance: true,
                NavigationIntent: CustomFollowerNavigationIntent.CatchUpToPlayer,
                ShouldRefreshHold: false),
            CustomFollowerBrainMode.HoldDefendLocal => new CustomFollowerMaintenanceDirective(
                SuppressLegacyMaintenance: true,
                NavigationIntent: null,
                ShouldRefreshHold: true),
            CustomFollowerBrainMode.HoldReturnToAnchor => new CustomFollowerMaintenanceDirective(
                SuppressLegacyMaintenance: true,
                NavigationIntent: null,
                ShouldRefreshHold: true),
            CustomFollowerBrainMode.CombatReturnToRange => new CustomFollowerMaintenanceDirective(
                SuppressLegacyMaintenance: true,
                NavigationIntent: CustomFollowerNavigationIntent.ReturnToCombatRange,
                ShouldRefreshHold: false),
            CustomFollowerBrainMode.RecoverNavigation => new CustomFollowerMaintenanceDirective(
                SuppressLegacyMaintenance: true,
                NavigationIntent: CustomFollowerNavigationIntent.RepathAndRecover,
                ShouldRefreshHold: false),
            _ => new CustomFollowerMaintenanceDirective(
                SuppressLegacyMaintenance: false,
                NavigationIntent: null,
                ShouldRefreshHold: false),
        };
    }
}

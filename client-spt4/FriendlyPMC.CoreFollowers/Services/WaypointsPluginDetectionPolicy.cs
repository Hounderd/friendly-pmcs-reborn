namespace FriendlyPMC.CoreFollowers.Services;

public static class WaypointsPluginDetectionPolicy
{
    public const string WaypointsPluginGuid = "xyz.drakia.waypoints";
    public const string LegacyWaypointsKey = "DrakiaXYZ-Waypoints";

    public static bool IsInstalled(IEnumerable<string> pluginKeys)
    {
        return pluginKeys.Any(key =>
            string.Equals(key, WaypointsPluginGuid, StringComparison.Ordinal)
            || string.Equals(key, LegacyWaypointsKey, StringComparison.Ordinal));
    }
}

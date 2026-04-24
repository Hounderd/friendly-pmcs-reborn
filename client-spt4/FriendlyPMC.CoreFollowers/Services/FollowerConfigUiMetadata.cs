namespace FriendlyPMC.CoreFollowers.Services;

internal sealed record FollowerConfigUiMetadata(
    string Section,
    string Key,
    string Category,
    string DisplayName,
    string Description,
    int Order,
    bool IsAdvanced = false);

internal static class FollowerConfigUiMetadataCatalog
{
    private static readonly IReadOnlyList<FollowerConfigUiMetadata> AllEntries =
    [
        new("Controls", "Follow", "1 Controls", "Follow Hotkey", "Hotkey used to issue the Follow order to managed followers.", 1600),
        new("Controls", "Hold", "1 Controls", "Hold Hotkey", "Hotkey used to issue the Hold order to managed followers.", 1500),
        new("Controls", "Combat", "1 Controls", "Combat Hotkey", "Hotkey used to issue the Combat order to managed followers.", 1400),
        new("Controls", "Heal", "1 Controls", "Heal Hotkey", "Hotkey used to issue the Heal order to managed followers.", 1300),

        new("Follower", "Follow Leash Distance", "2 Follower Behavior", "Follow Leash Distance", "Maximum distance followers should drift from the player before returning to escort position.", 1200),
        new("Follower", "Hold Radius Distance", "2 Follower Behavior", "Hold Radius Distance", "Maximum distance followers should roam from the hold anchor while defending.", 1100),
        new("Follower", "Follow Deadzone Distance", "2 Follower Behavior", "Follow Deadzone Distance", "Distance inside which followers stop making tiny follow corrections.", 1000),
        new("Follower", "Follow Catch-Up Distance", "2 Follower Behavior", "Follow Catch-Up Distance", "Distance at which followers switch into catch-up movement to rejoin the player faster.", 900),
        new("Follower", "Combat Max Range Distance", "2 Follower Behavior", "Combat Max Range Distance", "Maximum range followers should consider when engaging threats during active combat behavior.", 800),

        new("Follower Plates", "Enabled", "3 Follower Plates", "Show Follower Plates", "Show world-space follower nameplates for managed followers.", 700),
        new("Follower Plates", "Scale", "3 Follower Plates", "Follower Plate Scale", "Scale multiplier for follower nameplates.", 600),
        new("Follower Plates", "Max Distance", "3 Follower Plates", "Follower Plate Max Distance", "Maximum distance at which follower nameplates remain visible.", 500),
        new("Follower Plates", "Show Health Bar", "3 Follower Plates", "Show Follower Plate Health Bar", "Display a health bar on follower nameplates.", 400),
        new("Follower Plates", "Show Health Number", "3 Follower Plates", "Show Follower Plate Health Number", "Display numeric health on follower nameplates.", 300),
        new("Follower Plates", "Show Faction Badge", "3 Follower Plates", "Show Follower Plate Faction Badge", "Display faction badges on follower nameplates.", 200),
        new("Follower Plates", "Vertical Offset", "3 Follower Plates", "Follower Plate Vertical Offset", "Vertical world offset used when placing follower nameplates above a follower.", 100),

        new("Debug Followers", "Use Custom Brain", "9 Debug Followers", "Use Custom Brain", "Debug toggle that prefers the custom follower brain path when spawning debug followers.", 200, IsAdvanced: true),
        new("Debug Followers", "Fallback To Legacy Path", "9 Debug Followers", "Fallback To Legacy Path", "Debug toggle that allows falling back to the legacy debug follower control path.", 100, IsAdvanced: true),

        new("Debug", "Spawn Debug Follower", "10 Debug", "Spawn Debug Follower", "One-shot debug flag that queues a local debug follower spawn.", 300, IsAdvanced: true),
        new("Debug", "Spawn Debug Follower Hotkey", "10 Debug", "Spawn Debug Follower Hotkey", "Hotkey used to request a local debug follower spawn.", 200, IsAdvanced: true),
        new("Debug", "Auto Smoke Follower Profile On Friend Hydrate", "10 Debug", "Auto Smoke Follower Profile On Friend Hydrate", "Debug probe that automatically opens one follower profile after the friends list hydrates.", 100, IsAdvanced: true),
        new("Debug", "Enable Bot State Diagnostics", "10 Debug", "Enable Bot State Diagnostics", "Writes periodic bot brain snapshots to the BepInEx log. Leave disabled during normal raids.", 90, IsAdvanced: true),
        new("Debug", "Enable Combat Trace Diagnostics", "10 Debug", "Enable Combat Trace Diagnostics", "Writes high-volume follower combat and SAIN protection traces. Leave disabled during normal raids.", 80, IsAdvanced: true),
        new("Debug", "Enable Plate Diagnostics", "10 Debug", "Enable Plate Diagnostics", "Writes follower nameplate visibility diagnostics. Leave disabled during normal raids.", 70, IsAdvanced: true),
    ];

    public static IReadOnlyList<FollowerConfigUiMetadata> GetAll() => AllEntries;

    public static IReadOnlyList<FollowerConfigUiMetadata> GetPlayerFacingEntries() =>
        AllEntries.Where(entry => !entry.IsAdvanced).ToArray();

    public static FollowerConfigUiMetadata Get(string section, string key)
    {
        return AllEntries.First(entry =>
            string.Equals(entry.Section, section, StringComparison.Ordinal)
            && string.Equals(entry.Key, key, StringComparison.Ordinal));
    }
}

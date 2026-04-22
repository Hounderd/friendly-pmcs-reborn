using System;
using FriendlyPMC.CoreFollowers.Modules;
using FriendlyPMC.CoreFollowers.Patches;
using FriendlyPMC.CoreFollowers.Services;

#if SPT_CLIENT
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
#endif

namespace FriendlyPMC.CoreFollowers;

#if SPT_CLIENT
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class FriendlyPmcCoreFollowersPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "xyz.pit.friendlypmc.corefollowers";
    public const string PluginName = "FriendlyPMC.CoreFollowers";
    public const string PluginVersion = FriendlyPmcBuildVersion.Value;

    private ConfigEntry<KeyboardShortcut>? followKey;
    private ConfigEntry<KeyboardShortcut>? holdKey;
    private ConfigEntry<KeyboardShortcut>? combatKey;
    private ConfigEntry<KeyboardShortcut>? healKey;
    private ConfigEntry<float>? followLeashDistance;
    private ConfigEntry<float>? holdRadiusDistance;
    private ConfigEntry<float>? followDeadzoneDistance;
    private ConfigEntry<float>? followCatchUpDistance;
    private ConfigEntry<float>? combatMaxRangeDistance;
    private ConfigEntry<bool>? debugUseCustomBrain;
    private ConfigEntry<bool>? debugFallbackToLegacyPath;
    private ConfigEntry<bool>? showFollowerPlates;
    private ConfigEntry<float>? followerPlateScale;
    private ConfigEntry<float>? followerPlateMaxDistance;
    private ConfigEntry<bool>? followerPlateShowHealthBar;
    private ConfigEntry<bool>? followerPlateShowHealthNumber;
    private ConfigEntry<bool>? followerPlateShowFactionBadge;
    private ConfigEntry<float>? followerPlateVerticalOffset;
    private ConfigEntry<bool>? spawnDebugFollower;
    private ConfigEntry<KeyboardShortcut>? spawnDebugFollowerHotkey;
    private ConfigEntry<bool>? autoSmokeFollowerProfileOnFriendHydrate;
    private Task? debugSpawnTask;
    private Task? raidFollowerSpawnTask;
    private FollowerPlateManager? plateManager;

    internal static FriendlyPmcCoreFollowersPlugin Instance { get; private set; } = null!;

    public IFollowerApiClient ApiClient { get; private set; } = null!;

    public FollowerRegistry Registry { get; private set; } = null!;

    public FollowerRaidController RaidController { get; private set; } = null!;

    public FollowerCommandController CommandController { get; private set; } = null!;

    public DebugFollowerSpawnController DebugSpawnController { get; private set; } = null!;

    public FollowerInventoryScreenController InventoryScreenController { get; private set; } = null!;

    internal RaidFollowerSpawnService RaidFollowerSpawnService { get; private set; } = null!;

    internal BotDebugLogger DebugLogger { get; private set; } = null!;

    internal bool UseCustomBrainForDebugFollowers => debugUseCustomBrain?.Value ?? true;

    internal bool FallbackToLegacyPathForDebugFollowers => debugFallbackToLegacyPath?.Value ?? true;

    internal bool AutoSmokeFollowerProfileOnFriendHydrate => autoSmokeFollowerProfileOnFriendHydrate?.Value ?? false;

    internal bool IsWaypointsInstalled => WaypointsPluginDetectionPolicy.IsInstalled(Chainloader.PluginInfos.Keys);

    internal FollowerModeSettings ModeSettings =>
        new(
            followLeashDistance?.Value ?? FollowerModeSettings.DefaultFollowLeashDistanceMeters,
            holdRadiusDistance?.Value ?? FollowerModeSettings.DefaultHoldRadiusMeters,
            followDeadzoneDistance?.Value ?? FollowerModeSettings.DefaultFollowDeadzoneMeters,
            followCatchUpDistance?.Value ?? FollowerModeSettings.DefaultCatchUpDistanceMeters,
            combatMaxRangeDistance?.Value ?? FollowerModeSettings.DefaultCombatMaxRangeMeters);

    internal FollowerPlateSettings PlateSettings =>
        new(
            showFollowerPlates?.Value ?? true,
            followerPlateScale?.Value ?? FollowerPlateSettings.DefaultScale,
            followerPlateMaxDistance?.Value ?? FollowerPlateSettings.DefaultMaxDistanceMeters,
            followerPlateShowHealthBar?.Value ?? true,
            followerPlateShowHealthNumber?.Value ?? false,
            followerPlateShowFactionBadge?.Value ?? true,
            followerPlateVerticalOffset?.Value ?? FollowerPlateSettings.DefaultVerticalOffsetWorld);

    private static ConfigDescription Describe(
        string section,
        string key,
        AcceptableValueBase? acceptableValue = null)
    {
        var metadata = FollowerConfigUiMetadataCatalog.Get(section, key);
        return new ConfigDescription(
            metadata.Description,
            acceptableValue,
            new ConfigurationManagerAttributes
            {
                Category = metadata.Category,
                DispName = metadata.DisplayName,
                Order = metadata.Order,
                IsAdvanced = metadata.IsAdvanced,
            });
    }

    private void Awake()
    {
        Instance = this;
        ApiClient = new FollowerApiClient();
        Registry = new FollowerRegistry();
        RaidController = new FollowerRaidController(ApiClient, Registry, Logger.LogInfo);
        InventoryScreenController = new FollowerInventoryScreenController(
            new FollowerInventoryPresenter(ApiClient),
            runtimeViewFactory: null,
            logInfo: Logger.LogInfo,
            logError: LogPluginError);
        DebugSpawnController = new DebugFollowerSpawnController(new DebugFollowerSpawnService());
        RaidFollowerSpawnService = new RaidFollowerSpawnService();
        DebugLogger = new BotDebugLogger(Registry, Logger.LogInfo);

        followKey = Config.Bind("Controls", "Follow", new KeyboardShortcut(KeyCode.F6), Describe("Controls", "Follow"));
        holdKey = Config.Bind("Controls", "Hold", new KeyboardShortcut(KeyCode.F7), Describe("Controls", "Hold"));
        combatKey = Config.Bind("Controls", "Combat", new KeyboardShortcut(KeyCode.F8), Describe("Controls", "Combat"));
        healKey = Config.Bind("Controls", "Heal", new KeyboardShortcut(KeyCode.F10), Describe("Controls", "Heal"));
        CommandController = new FollowerCommandController(Registry, new FollowerCommandBindings("F6", "F7", "F8", "F10"));
        followLeashDistance = Config.Bind("Follower", "Follow Leash Distance", FollowerModeSettings.DefaultFollowLeashDistanceMeters, Describe("Follower", "Follow Leash Distance"));
        holdRadiusDistance = Config.Bind("Follower", "Hold Radius Distance", FollowerModeSettings.DefaultHoldRadiusMeters, Describe("Follower", "Hold Radius Distance"));
        followDeadzoneDistance = Config.Bind("Follower", "Follow Deadzone Distance", FollowerModeSettings.DefaultFollowDeadzoneMeters, Describe("Follower", "Follow Deadzone Distance"));
        followCatchUpDistance = Config.Bind("Follower", "Follow Catch-Up Distance", FollowerModeSettings.DefaultCatchUpDistanceMeters, Describe("Follower", "Follow Catch-Up Distance"));
        combatMaxRangeDistance = Config.Bind("Follower", "Combat Max Range Distance", FollowerModeSettings.DefaultCombatMaxRangeMeters, Describe("Follower", "Combat Max Range Distance"));
        debugUseCustomBrain = Config.Bind("Debug Followers", "Use Custom Brain", true, Describe("Debug Followers", "Use Custom Brain"));
        debugFallbackToLegacyPath = Config.Bind("Debug Followers", "Fallback To Legacy Path", true, Describe("Debug Followers", "Fallback To Legacy Path"));
        showFollowerPlates = Config.Bind("Follower Plates", "Enabled", true, Describe("Follower Plates", "Enabled"));
        followerPlateScale = Config.Bind("Follower Plates", "Scale", FollowerPlateSettings.DefaultScale, Describe("Follower Plates", "Scale"));
        followerPlateMaxDistance = Config.Bind("Follower Plates", "Max Distance", FollowerPlateSettings.DefaultMaxDistanceMeters, Describe("Follower Plates", "Max Distance"));
        followerPlateShowHealthBar = Config.Bind("Follower Plates", "Show Health Bar", true, Describe("Follower Plates", "Show Health Bar"));
        followerPlateShowHealthNumber = Config.Bind("Follower Plates", "Show Health Number", false, Describe("Follower Plates", "Show Health Number"));
        followerPlateShowFactionBadge = Config.Bind("Follower Plates", "Show Faction Badge", true, Describe("Follower Plates", "Show Faction Badge"));
        followerPlateVerticalOffset = Config.Bind("Follower Plates", "Vertical Offset", FollowerPlateSettings.DefaultVerticalOffsetWorld, Describe("Follower Plates", "Vertical Offset"));
        spawnDebugFollower = Config.Bind("Debug", "Spawn Debug Follower", false, Describe("Debug", "Spawn Debug Follower"));
        spawnDebugFollowerHotkey = Config.Bind("Debug", "Spawn Debug Follower Hotkey", new KeyboardShortcut(KeyCode.F9), Describe("Debug", "Spawn Debug Follower Hotkey"));
        autoSmokeFollowerProfileOnFriendHydrate = Config.Bind("Debug", "Auto Smoke Follower Profile On Friend Hydrate", false, Describe("Debug", "Auto Smoke Follower Profile On Friend Hydrate"));
        if (autoSmokeFollowerProfileOnFriendHydrate.Value)
        {
            autoSmokeFollowerProfileOnFriendHydrate.Value = false;
            Config.Save();
            Logger.LogInfo("Disabled legacy follower social startup smoke debug flag");
        }

        new RaidStartPatch().Enable();
        new BotsControllerStatePatch().Enable();
        new BotsEventsControllerSpawnPatch().Enable();
        new FriendlyPlayerHostilityPatch().Enable();
        new FriendlyEnemyMemoryPatch().Enable();
        new FriendlyEnemyControllerPatch().Enable();
        new DebugFollowerBossSetupPatch().Enable();
        new SainIsBotExcludedByProfileIdPatch().Enable();
        new SainIsBotExcludedByOwnerPatch().Enable();
        new SainIsDisabledForBotOwnerPatch().Enable();
        new SainIsDisabledForPlayerPatch().Enable();
        new SainEnemyCheckAddPatch().Enable();
        new SainEnemyManualUpdatePatch().Enable();
        new RaidEndPatch().Enable();
        new PlayerInteractRecruitPatch().Enable();
        new BotOwnerUpdatePatch().Enable();
        new FollowerPhraseCommandPatch().Enable();
        new SocialNetworkClassInitPatch().Enable();
        new SocialNetworkClassSendPatch().Enable();
        new SocialNetworkClassFriendsListHydratedPatch().Enable();
        new DialogueInteractionProfileOpenPatch().Enable();
        new FriendInteractionProfileOpenPatch().Enable();
        new ProfileEndpointGetOtherPlayerProfilePatch().Enable();
        new ItemUiContextShowPlayerProfileScreenPatch().Enable();
        new OtherPlayerProfileScreenShowPatch().Enable();
        new OtherPlayerProfileScreenVisualShowPatch().Enable();
        new OtherPlayerProfileScreenControllerDisplayPatch().Enable();
        new OtherPlayerProfileScreenControllerShowScreenPatch().Enable();
        new OtherPlayerProfileScreenControllerShowAsyncPatch().Enable();
        new OtherPlayerProfileScreenControllerClosePatch().Enable();
        new PlayerShotTrackingPatch().Enable();
        new HostileShotStimulusPatch().Enable();
        new BulletNearMissStimulusPatch().Enable();
        new HostileFootstepStimulusPatch().Enable();
        new HostileVoiceLineStimulusPatch().Enable();
        /*
        if (LootingLayerCompatibilityPatch.IsAvailable)
        {
            new LootingLayerCompatibilityPatch().Enable();
            new LootingLayerEndingCompatibilityPatch().Enable();
        }
        */
        FriendlyFollowerBigBrainBootstrap.RegisterLayers(Logger.LogInfo);
        plateManager = gameObject.AddComponent<FollowerPlateManager>();

        Logger.LogInfo("FriendlyPMC core follower plugin loaded");
    }

    private void Update()
    {
        TryQueueDebugSpawn();
        TryLogCommand(followKey, "F6");
        TryLogCommand(holdKey, "F7");
        TryLogCommand(combatKey, "F8");
        TryLogCommand(healKey, "F10");
        DebugLogger.Tick();
    }

    private void TryQueueDebugSpawn()
    {
        var spawnRequested = false;

        if (spawnDebugFollower is { Value: true })
        {
            spawnRequested = true;
            spawnDebugFollower.Value = false;
            Config.Save();
            Logger.LogInfo("Debug follower spawn requested from config");
        }

        if (spawnDebugFollowerHotkey is not null && spawnDebugFollowerHotkey.Value.IsUp())
        {
            spawnRequested = true;
            Logger.LogInfo("Debug follower spawn requested from hotkey");
        }

        if (!spawnRequested)
        {
            return;
        }

        DebugSpawnController.RequestSpawn();
        if (debugSpawnTask is not null && !debugSpawnTask.IsCompleted)
        {
            return;
        }

        debugSpawnTask = ProcessDebugSpawnRequestsAsync();
    }

    private async Task ProcessDebugSpawnRequestsAsync()
    {
        try
        {
            while (await DebugSpawnController.ProcessPendingSpawnAsync())
            {
            }
        }
        catch (Exception ex)
        {
            LogPluginError("Failed to spawn debug follower", ex);
        }
    }

    private void TryLogCommand(ConfigEntry<KeyboardShortcut>? shortcut, string keyToken)
    {
        if (shortcut is null || !shortcut.Value.IsUp())
        {
            return;
        }

        var command = CommandController.HandleKeyPress(keyToken);
        if (command.HasValue)
        {
            Logger.LogInfo($"Follower command requested: source=hotkey, trigger={keyToken}, command={command.Value}");
        }
    }

    internal void RegisterRuntimeFollower(IFollowerRuntimeHandle follower)
    {
        Registry.RegisterRuntime(follower);
    }

    internal void RegisterRecruit(IFollowerRuntimeHandle follower)
    {
        Registry.RegisterRuntime(follower);
        ApiClient.RegisterRecruitAsync(follower.CaptureSnapshot()).GetAwaiter().GetResult();
    }

    internal void QueueRaidFollowerSpawn()
    {
        if (!RaidController.TryBeginPendingFollowerSpawn())
        {
            return;
        }

        if (raidFollowerSpawnTask is not null && !raidFollowerSpawnTask.IsCompleted)
        {
            return;
        }

        raidFollowerSpawnTask = ProcessRaidFollowerSpawnAsync();
    }

    private async Task ProcessRaidFollowerSpawnAsync()
    {
        try
        {
            await RaidFollowerSpawnService.SpawnPendingFollowersAsync();
        }
        catch (Exception ex)
        {
            RaidController.AllowPendingFollowerSpawnRetry();
            LogPluginError("Failed to spawn persisted raid followers", ex);
        }
    }

    internal void LogPluginInfo(string message)
    {
        Logger.LogInfo(message);
    }

    internal void LogPluginError(string message, Exception exception)
    {
        Logger.LogError(message);
        Logger.LogError(exception);
    }
}
#else
public sealed class FriendlyPmcCoreFollowersPlugin
{
    public const string PluginVersion = FriendlyPmcBuildVersion.Value;

    public FriendlyPmcCoreFollowersPlugin()
    {
        ApiClient = new FollowerApiClient();
        Registry = new FollowerRegistry();
        RaidController = new FollowerRaidController(ApiClient, Registry, _ => { });
        CommandController = new FollowerCommandController(Registry, new FollowerCommandBindings("F6", "F7", "F8", "F10"));
        DebugSpawnController = new DebugFollowerSpawnController(new DebugFollowerSpawnService());
        DebugLogger = new BotDebugLogger(Registry, _ => { });
    }

    public IFollowerApiClient ApiClient { get; }

    public FollowerRegistry Registry { get; }

    public FollowerRaidController RaidController { get; }

    public FollowerCommandController CommandController { get; }

    public DebugFollowerSpawnController DebugSpawnController { get; }

    internal BotDebugLogger DebugLogger { get; }

    internal FollowerModeSettings ModeSettings => new();

    internal bool UseCustomBrainForDebugFollowers => true;

    internal bool FallbackToLegacyPathForDebugFollowers => true;

    internal bool IsWaypointsInstalled => false;

    internal void RegisterRuntimeFollower(IFollowerRuntimeHandle follower)
    {
        Registry.RegisterRuntime(follower);
    }

    internal void RegisterRecruit(IFollowerRuntimeHandle follower)
    {
        Registry.RegisterRuntime(follower);
    }
}
#endif

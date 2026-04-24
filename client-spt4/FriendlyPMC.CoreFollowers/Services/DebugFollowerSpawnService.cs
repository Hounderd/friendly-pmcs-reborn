using FriendlyPMC.CoreFollowers.Models;
using FriendlyPMC.CoreFollowers.Modules;
using FriendlyPMC.CoreFollowers.Threading;

#if SPT_CLIENT
using System.Diagnostics;
using System.Threading;
using Comfort.Common;
using EFT;
using FriendlyPMC.CoreFollowers.Patches;
using HarmonyLib;
using UnityEngine;
#endif

namespace FriendlyPMC.CoreFollowers.Services;

#if SPT_CLIENT
internal sealed class DebugFollowerSpawnService : IDebugFollowerSpawnService
{
    private static readonly System.Reflection.FieldInfo BotCreatorField = AccessTools.Field(typeof(BotSpawner), "BotCreator");
    private static readonly System.Reflection.FieldInfo DeadBodiesControllerField = AccessTools.Field(typeof(BotSpawner), "DeadBodiesController");
    private static readonly System.Reflection.FieldInfo AllPlayersField = AccessTools.Field(typeof(BotSpawner), "AllPlayers");
    private static readonly System.Reflection.FieldInfo BotPresetsField = AccessTools.Field(typeof(BotCreatorClass), "Ginterface21_0");
    private static readonly System.Reflection.FieldInfo SessionField = AccessTools.Field(typeof(BotsPresets), "ISession");
    private static readonly System.Reflection.MethodInfo FinalizeSpawnMethod = AccessTools.Method(typeof(BotSpawner), "method_11");
    private static readonly System.Reflection.MethodInfo ExistingBotsMethod = AccessTools.Method(typeof(BotSpawner), "method_5");

    public async Task SpawnAsync()
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            throw new InvalidOperationException("FriendlyPMC plugin instance is unavailable");
        }

        if (GamePlayerOwner.MyPlayer is not { } localPlayer)
        {
            throw new InvalidOperationException("Debug follower spawn requires an active raid");
        }

        var unityContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("Debug follower spawn requires a Unity synchronization context");
        var controlDecision = DebugSpawnFollowerBrainHost.ResolveControlPath(
            plugin.UseCustomBrainForDebugFollowers,
            plugin.FallbackToLegacyPathForDebugFollowers,
            plugin.IsWaypointsInstalled);
        if (controlDecision.ControlPath == DebugSpawnFollowerControlPath.Abort)
        {
            throw new InvalidOperationException("Custom debug follower brain requires Waypoints and fallback is disabled");
        }

        if (BotsControllerStatePatch.ActiveController is not { } botsController)
        {
            throw new InvalidOperationException("Bots controller is unavailable for debug follower spawn");
        }

        var botSpawner = botsController.BotSpawner;
        var botCreator = BotCreatorField.GetValue(botSpawner) as IBotCreator
            ?? throw new InvalidOperationException("Bot creator is unavailable");

        var spawnPosition = GetSpawnPosition(localPlayer);
        var botZone = botsController.GetClosestZone(spawnPosition, out _)
            ?? throw new InvalidOperationException("Unable to find a bot zone near the player");
        var closestCorePoint = botsController.CoversData.GetClosest(spawnPosition).CorePointInGame;
        if (closestCorePoint is null)
        {
            throw new InvalidOperationException("Unable to find a valid core point for follower spawn");
        }

        var spawnParams = new BotSpawnParams
        {
            ShallBeGroup = new ShallBeGroupParams(true, false, 2),
        };

        var role = ResolveRole(localPlayer.Side);
        var profileData = new BotProfileDataClass(localPlayer.Side, role, BotDifficulty.impossible, 0f, spawnParams, false);
        var profile = await LoadGeneratedProfileAsync(botCreator, profileData);
        plugin.LogPluginInfo(
            $"Debug spawn context: playerSide={localPlayer.Side}, role={role}, playerGroup={localPlayer.GroupId}, playerTeam={localPlayer.Profile.Info.TeamId}");
        plugin.LogPluginInfo($"Loaded debug follower profile {profile.Info.Nickname} (profileId={profile.ProfileId}, accountId={profile.AccountId})");
        await SynchronizationContextBridge.ResumeOnAsync(unityContext);
        var preloadCompleted = await BundlePreloadGuard.RunAsync(() => PreloadProfileBundlesAsync(profile));
        if (!preloadCompleted)
        {
            plugin.LogPluginInfo($"Debug follower bundle preload canceled for {profile.Info.Nickname}; continuing with spawn");
        }

        var botCreationData = BotCreationDataClass.CreateWithoutProfile(profileData);
        plugin.LogPluginInfo($"Prepared debug follower profile {profile.Info.Nickname} (profileId={profile.ProfileId}, accountId={profile.AccountId})");

        botCreationData.AddPosition(spawnPosition, closestCorePoint.Id);
        botCreationData.AddProfile(profile);
        profile.Info.Side = localPlayer.Side;
        profile.Info.GroupId = localPlayer.GroupId;
        profile.Info.TeamId = localPlayer.Profile.Info.TeamId;
        plugin.Registry.MarkExpectedRuntimeProfileId(profile.ProfileId);
        plugin.LogPluginInfo(
            $"Prepared profile identity: botSide={profile.Info.Side}, botGroup={profile.Info.GroupId}, botTeam={profile.Info.TeamId}");

        var identitySnapshot = new FollowerSnapshotDto(
            FollowerIdentityResolver.Resolve(profile.ProfileId, profile.AccountId),
            profile.Info.Nickname,
            localPlayer.Side.ToString(),
            profile.Info.Level,
            profile.Experience,
            new Dictionary<string, int>(),
            Array.Empty<string>(),
            new Dictionary<string, int>
            {
                ["Head"] = 35,
            },
            new Dictionary<string, int>
            {
                ["Head"] = 35,
            },
            null);

        try
        {
            await botCreator.ActivateBot(
                profile,
                botCreationData.GetPosition(),
                botZone,
                false,
                (owner, zone) => CreateFollowerGroup(plugin, botSpawner, botsController, localPlayer, owner, zone),
                owner => FinalizeFollowerSpawn(plugin, botSpawner, botCreationData, localPlayer, owner, identitySnapshot, controlDecision),
                CancellationToken.None);
        }
        catch
        {
            plugin.Registry.UnmarkExpectedRuntimeProfileId(profile.ProfileId);
            throw;
        }

        plugin.LogPluginInfo($"ActivateBot completed for debug follower profile {profile.Info.Nickname}");
    }

    private static async Task<Profile> LoadGeneratedProfileAsync(IBotCreator botCreator, BotProfileDataClass profileData)
    {
        var botPresets = BotPresetsField.GetValue(botCreator) as BotsPresets
            ?? throw new InvalidOperationException("Bot presets are unavailable");
        var profileEndpoint = SessionField.GetValue(botPresets) as ProfileEndpointFactoryAbstractClass
            ?? throw new InvalidOperationException("Profile endpoint is unavailable");

        var generatedProfiles = await profileEndpoint.LoadBots(profileData.PrepareToLoadBackend(1).ToList());
        var profile = generatedProfiles?.FirstOrDefault();
        return profile ?? throw new InvalidOperationException("Backend bot generation returned no profiles");
    }

    private static async Task PreloadProfileBundlesAsync(Profile profile)
    {
        var prefabPaths = profile.GetAllPrefabPaths(false).ToArray();
        if (prefabPaths.Length == 0)
        {
            return;
        }

        await Singleton<PoolManagerClass>.Instance.LoadBundlesAndCreatePools(
            PoolManagerClass.PoolsCategory.Raid,
            PoolManagerClass.AssemblyType.Local,
            prefabPaths,
            JobPriorityClass.General,
            null,
            PoolManagerClass.DefaultCancellationToken);
    }

    private static WildSpawnType ResolveRole(EPlayerSide side)
    {
        return side switch
        {
            EPlayerSide.Bear => WildSpawnType.pmcBEAR,
            EPlayerSide.Usec => WildSpawnType.pmcUSEC,
            _ => WildSpawnType.assault,
        };
    }

    private static Vector3 GetSpawnPosition(Player localPlayer)
    {
        var playerTransform = localPlayer.Transform;
        return playerTransform.position + (playerTransform.forward * 2f) + (playerTransform.right * 1.5f);
    }

    private static BotsGroup CreateFollowerGroup(
        FriendlyPmcCoreFollowersPlugin plugin,
        BotSpawner botSpawner,
        BotsController botsController,
        Player localPlayer,
        BotOwner owner,
        BotZone zone)
    {
        try
        {
            plugin.LogPluginInfo($"CreateFollowerGroup invoked for {owner.Profile.Info.Nickname}");
            PrepareOwnerIdentity(owner, localPlayer);

            var deadBodiesController = DeadBodiesControllerField.GetValue(botSpawner) as DeadBodiesController
                ?? throw new InvalidOperationException("Dead bodies controller is unavailable");
            var allPlayers = AllPlayersField.GetValue(botSpawner) as List<Player> ?? new List<Player>();
            if (!allPlayers.Contains(localPlayer))
            {
                allPlayers.Add(localPlayer);
            }

            var activeEnemies = GetActiveEnemies(botSpawner, owner);
            var group = new PlayerFollowerBotsGroup(zone, botsController.BotGame, owner, activeEnemies, deadBodiesController, allPlayers, localPlayer);
            plugin.LogPluginInfo($"CreateFollowerGroup completed for {owner.Profile.Info.Nickname}");
            return group;
        }
        catch (Exception ex)
        {
            plugin.LogPluginError("CreateFollowerGroup failed for debug follower", ex);
            throw;
        }
    }

    private static void FinalizeFollowerSpawn(
        FriendlyPmcCoreFollowersPlugin plugin,
        BotSpawner botSpawner,
        BotCreationDataClass botCreationData,
        Player localPlayer,
        BotOwner owner,
        FollowerSnapshotDto identitySnapshot,
        DebugSpawnFollowerControlDecision controlDecision)
    {
        try
        {
            PrepareFriendlyOwner(owner, localPlayer);
            plugin.LogPluginInfo($"Finalizing debug follower spawn for {owner.Profile.Info.Nickname}");
            var stopwatch = Stopwatch.StartNew();
            var shallBeGroup = botCreationData.SpawnParams?.ShallBeGroup is not null;

            FinalizeSpawnMethod.Invoke(
                botSpawner,
                new object[]
                {
                    owner,
                botCreationData,
                    new Action<BotOwner>(spawnedOwner => BindFollower(plugin, localPlayer, spawnedOwner, identitySnapshot, controlDecision)),
                    shallBeGroup,
                    stopwatch,
                });
        }
        catch (Exception ex)
        {
            plugin.LogPluginError("FinalizeFollowerSpawn failed for debug follower", ex);
            throw;
        }
    }

    private static void BindFollower(
        FriendlyPmcCoreFollowersPlugin plugin,
        Player localPlayer,
        BotOwner spawnedOwner,
        FollowerSnapshotDto identitySnapshot,
        DebugSpawnFollowerControlDecision controlDecision)
    {
        try
        {
            var runtimeFollower = new BotOwnerFollowerRuntimeHandle(spawnedOwner, identitySnapshot);
            plugin.RegisterRuntimeFollower(runtimeFollower);
            plugin.Registry.SetControlPathRuntime(
                runtimeFollower.Aid,
                CustomFollowerControlPathRuntime.Create(controlDecision.ControlPath, controlDecision.AbortReason));
            var boundCustomBrainSession = DebugSpawnFollowerSessionBinder.Bind(
                plugin.Registry,
                runtimeFollower.Aid,
                controlDecision.ControlPath);
            FollowerLootingRuntimeDisabler.DisableForFollower(spawnedOwner, plugin.LogPluginInfo);
            PrepareFriendlyOwner(spawnedOwner, localPlayer);
            plugin.LogPluginInfo(
                $"Debug follower control path selected: follower={spawnedOwner.Profile.Info.Nickname}, aid={runtimeFollower.Aid}, path={controlDecision.ControlPath}, waypointsAvailable={plugin.IsWaypointsInstalled}, abortReason={controlDecision.AbortReason ?? "None"}");
            if (controlDecision.ControlPath == DebugSpawnFollowerControlPath.CustomBrain)
            {
                plugin.LogPluginInfo(
                    $"Custom debug follower brain selected for {spawnedOwner.Profile.Info.Nickname}; customSessionBound={boundCustomBrainSession}");
            }
            plugin.LogPluginInfo(
                $"Pre-bind hostility: isEnemy={spawnedOwner.BotsGroup?.IsPlayerEnemy(localPlayer)}, isNeutral={spawnedOwner.BotsGroup?.Neutrals.ContainsKey(localPlayer)}, followerAid={runtimeFollower.Aid}, ownerProfileId={spawnedOwner.ProfileId}");
            spawnedOwner.Memory.DeleteInfoAboutEnemy(localPlayer);
            spawnedOwner.BotsGroup?.RemoveEnemy(localPlayer);
            spawnedOwner.BotsGroup?.AddNeutral(localPlayer);
            spawnedOwner.BotsGroup?.AddAlly(localPlayer);
            spawnedOwner.BotsGroup?.AddMember(spawnedOwner, false);
            RebindBotToFollowerGroup(spawnedOwner);
            ClearKnownEnemies(spawnedOwner);
            spawnedOwner.Memory.GoalEnemy = null;
            EnsureBotFollowerComponent(plugin, spawnedOwner, localPlayer, controlDecision.ControlPath);
            spawnedOwner.GetPlayer.ActiveHealthController.RestoreFullHealth();
            plugin.LogPluginInfo(
                $"Post-bind hostility: isEnemy={spawnedOwner.BotsGroup?.IsPlayerEnemy(localPlayer)}, isNeutral={spawnedOwner.BotsGroup?.Neutrals.ContainsKey(localPlayer)}");
            plugin.LogPluginInfo(
                $"Bound follower identity: ownerSide={spawnedOwner.Side}, ownerGroup={spawnedOwner.GroupId}, ownerTeam={spawnedOwner.TeamId}, playerSide={localPlayer.Side}, playerGroup={localPlayer.GroupId}, playerTeam={localPlayer.Profile.Info.TeamId}");
            plugin.LogPluginInfo(
                $"BotFollower state: haveBoss={spawnedOwner.BotFollower?.HaveBoss}, bossToFollow={spawnedOwner.BotFollower?.BossToFollow?.Player()?.Profile?.Nickname}");
            plugin.LogPluginInfo($"Spawned debug follower {spawnedOwner.Profile.Info.Nickname}");
        }
        catch (Exception ex)
        {
            plugin.LogPluginError("BindFollower failed for debug follower", ex);
            throw;
        }
    }

    private static void PrepareFriendlyOwner(BotOwner owner, Player localPlayer)
    {
        PrepareOwnerIdentity(owner, localPlayer);

        owner.Memory?.DeleteInfoAboutEnemy(localPlayer);

        var settings = owner.Settings;
        if (settings is null)
        {
            return;
        }

        var policy = FollowerMindPolicy.For(ToFollowerSide(localPlayer.Side));

        settings.FileSettings.Mind.ENEMY_BY_GROUPS_PMC_PLAYERS = policy.EnemyByGroupsPmcPlayers;
        settings.FileSettings.Mind.ENEMY_BY_GROUPS_SAVAGE_PLAYERS = policy.EnemyByGroupsSavagePlayers;
        settings.FileSettings.Mind.USE_ADD_TO_ENEMY_VALIDATION = policy.UseAddToEnemyValidation;
        if (policy.ClearValidReasonsToAddEnemy)
        {
            settings.FileSettings.Mind.VALID_REASONS_TO_ADD_ENEMY = Array.Empty<EBotEnemyCause>();
        }

        settings.FileSettings.Mind.CAN_EXECUTE_REQUESTS = true;
        settings.FileSettings.Mind.CAN_RECEIVE_PLAYER_REQUESTS_BEAR = localPlayer.Side == EPlayerSide.Bear;
        settings.FileSettings.Mind.CAN_RECEIVE_PLAYER_REQUESTS_USEC = localPlayer.Side == EPlayerSide.Usec;
        settings.FileSettings.Mind.CAN_RECEIVE_PLAYER_REQUESTS_SAVAGE = localPlayer.Side == EPlayerSide.Savage;
        settings.FileSettings.Mind.CHANCE_FUCK_YOU_ON_CONTACT_100 = 0;
        settings.FileSettings.Mind.REVENGE_TO_GROUP = true;
        settings.FileSettings.Mind.REVENGE_FOR_SAVAGE_PLAYERS = false;

        ApplyWarnBehaviour(policy.DefaultBearBehaviour, behaviour => settings.FileSettings.Mind.DEFAULT_BEAR_BEHAVIOUR = behaviour);
        ApplyWarnBehaviour(policy.DefaultUsecBehaviour, behaviour => settings.FileSettings.Mind.DEFAULT_USEC_BEHAVIOUR = behaviour);
        ApplyWarnBehaviour(policy.DefaultSavageBehaviour, behaviour => settings.FileSettings.Mind.DEFAULT_SAVAGE_BEHAVIOUR = behaviour);

        var enemyTypes = settings.GetEnemyBotTypes();
        if (enemyTypes is null)
        {
            return;
        }

        foreach (var role in FollowerEnemyRolePolicy.GetRequiredHostileRoles(ToFollowerSide(localPlayer.Side)))
        {
            var wildSpawnType = role switch
            {
                EftWildSpawnRole.UsecPmc => WildSpawnType.pmcUSEC,
                EftWildSpawnRole.BearPmc => WildSpawnType.pmcBEAR,
                EftWildSpawnRole.Scav => WildSpawnType.assault,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
            };

            if (!enemyTypes.Contains(wildSpawnType))
            {
                enemyTypes.Add(wildSpawnType);
            }
        }
    }

    private static List<BotOwner> GetActiveEnemies(BotSpawner botSpawner, BotOwner owner)
    {
        if (ExistingBotsMethod.Invoke(botSpawner, new object[] { owner }) is not IEnumerable<BotOwner> existingBots)
        {
            return new List<BotOwner>();
        }

        return existingBots
            .Where(candidate => candidate is not null && !candidate.IsDead && candidate != owner)
            .ToList();
    }

    private static void ClearKnownEnemies(BotOwner owner)
    {
        var knownEnemies = owner.EnemiesController?.EnemyInfos?.ToList();
        if (knownEnemies is null)
        {
            return;
        }

        foreach (var enemy in knownEnemies)
        {
            owner.Memory.DeleteInfoAboutEnemy(enemy.Key);
        }
    }

    private static void RebindBotToFollowerGroup(BotOwner owner)
    {
        var requestController = owner.BotRequestController;
        var botsGroup = owner.BotsGroup;
        if (requestController is null || botsGroup is null)
        {
            return;
        }

        owner.Memory.BotsGroup_0 = botsGroup;
        requestController.GroupRequestController_1 = botsGroup.RequestsController;
    }

    private static void EnsureBotFollowerComponent(
        FriendlyPmcCoreFollowersPlugin plugin,
        BotOwner owner,
        Player localPlayer,
        DebugSpawnFollowerControlPath controlPath)
    {
        var aiBossPlayer = localPlayer.AIData?.AIBossPlayer;
        var source = FollowerBossBindingPolicy.Select(
            playerImplementsBossToFollow: localPlayer is IBossToFollow,
            aiBossPlayerAvailable: aiBossPlayer is not null);

        var bossToFollow = source switch
        {
            FollowerBossBindingSource.AiBossPlayer => aiBossPlayer as IBossToFollow,
            FollowerBossBindingSource.Player => localPlayer as IBossToFollow,
            _ => null,
        };

        if (bossToFollow is null)
        {
            plugin.LogPluginInfo("No IBossToFollow source is available for the local player");
            return;
        }

        var botFollower = owner.BotFollower ?? BotFollower.Create(owner);
        if (owner.BotFollower is null)
        {
            owner.BotFollower = botFollower;
        }

        if (botFollower.PatrolDataFollower is null)
        {
            botFollower.Activate();
        }

        try
        {
            botFollower.SetToFollow(bossToFollow, 0, true);
            if (DebugSpawnFollowerLegacySeedPolicy.ShouldSeedLegacyFollowOrder(controlPath))
            {
                FollowerMovementStateApplier.TrySeedMovementOrder(owner, localPlayer, FollowerCommand.Follow);
            }
            plugin.LogPluginInfo($"Bound BotFollower using {source} via SetToFollow");
        }
        catch (NullReferenceException ex)
        {
            plugin.LogPluginInfo($"SetToFollow failed for {owner.Profile.Info.Nickname}; using manual follower bind fallback: {ex.Message}");
            BindBotFollowerManually(owner, localPlayer, bossToFollow, botFollower, controlPath);
            plugin.LogPluginInfo($"Bound BotFollower using {source} via manual fallback");
        }
    }

    private static void BindBotFollowerManually(
        BotOwner owner,
        Player localPlayer,
        IBossToFollow bossToFollow,
        BotFollower botFollower,
        DebugSpawnFollowerControlPath controlPath)
    {
        if (!bossToFollow.Followers.Contains(owner))
        {
            bossToFollow.Followers.Add(owner);
        }

        botFollower.PatrolDataFollower?.InitPlayer(localPlayer);

        var index = bossToFollow.Followers.IndexOf(owner);
        if (index < 0)
        {
            index = 0;
        }

        botFollower.Index = index;
        botFollower.PatrolDataFollower?.SetIndex(index);
        botFollower.BossToFollow = bossToFollow;
        if (DebugSpawnFollowerLegacySeedPolicy.ShouldSeedLegacyFollowOrder(controlPath))
        {
            FollowerMovementStateApplier.TrySeedMovementOrder(owner, localPlayer, FollowerCommand.Follow);
        }
    }

    private static void ApplyWarnBehaviour(FollowerWarnBehaviour? behaviour, Action<EWarnBehaviour> apply)
    {
        if (behaviour is not FollowerWarnBehaviour.AlwaysEnemies)
        {
            return;
        }

        apply(EWarnBehaviour.AlwaysEnemies);
    }

    private static FollowerSide ToFollowerSide(EPlayerSide side)
    {
        return side switch
        {
            EPlayerSide.Bear => FollowerSide.Bear,
            EPlayerSide.Usec => FollowerSide.Usec,
            _ => FollowerSide.Savage,
        };
    }

    private static void PrepareOwnerIdentity(BotOwner owner, Player localPlayer)
    {
        if (owner.Profile?.Info is { } ownerInfo)
        {
            ownerInfo.Side = localPlayer.Side;
            ownerInfo.GroupId = localPlayer.GroupId;
            ownerInfo.TeamId = localPlayer.Profile.Info.TeamId;
        }

        if (owner.GetPlayer?.Profile?.Info is { } playerInfo)
        {
            playerInfo.Side = localPlayer.Side;
            playerInfo.GroupId = localPlayer.GroupId;
            playerInfo.TeamId = localPlayer.Profile.Info.TeamId;
        }
    }

}
#else
internal sealed class DebugFollowerSpawnService : IDebugFollowerSpawnService
{
    public Task SpawnAsync()
    {
        return Task.CompletedTask;
    }
}
#endif

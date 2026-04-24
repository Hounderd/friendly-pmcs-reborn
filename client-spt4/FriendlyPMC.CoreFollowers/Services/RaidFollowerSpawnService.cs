#if SPT_CLIENT
using System.Diagnostics;
using System.IO;
using System.Threading;
using BepInEx;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using FriendlyPMC.CoreFollowers.Models;
using FriendlyPMC.CoreFollowers.Modules;
using FriendlyPMC.CoreFollowers.Patches;
using FriendlyPMC.CoreFollowers.Threading;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class RaidFollowerSpawnService
{
    private static readonly System.Reflection.FieldInfo BotPresetsField = AccessTools.Field(typeof(BotCreatorClass), "Ginterface21_0");
    private static readonly System.Reflection.FieldInfo SessionField = AccessTools.Field(typeof(BotsPresets), "ISession");
    private static readonly System.Reflection.MethodInfo FinalizeSpawnMethod = AccessTools.Method(typeof(BotSpawner), "method_11");
    private static readonly System.Reflection.MethodInfo ExistingBotsMethod = AccessTools.Method(typeof(BotSpawner), "method_5");
    public async Task SpawnPendingFollowersAsync()
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            throw new InvalidOperationException("FriendlyPMC plugin instance is unavailable");
        }

        if (GamePlayerOwner.MyPlayer is not { } localPlayer)
        {
            throw new InvalidOperationException("Follower raid spawn requires an active raid");
        }

        var unityContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("Follower raid spawn requires a Unity synchronization context");
        var controlDecision = DebugSpawnFollowerBrainHost.ResolveControlPath(
            plugin.UseCustomBrainForDebugFollowers,
            plugin.FallbackToLegacyPathForDebugFollowers,
            plugin.IsWaypointsInstalled);
        if (controlDecision.ControlPath == DebugSpawnFollowerControlPath.Abort)
        {
            plugin.LogPluginInfo("Follower raid spawn aborted: custom brain requires Waypoints and legacy fallback is disabled");
            return;
        }

        if (BotsControllerStatePatch.ActiveController is not { } botsController)
        {
            throw new InvalidOperationException("Bots controller is unavailable for follower raid spawn");
        }

        var botSpawner = botsController.BotSpawner;
        var botCreator = botSpawner.BotCreator
            ?? throw new InvalidOperationException("Bot creator is unavailable");
        var pendingFollowers = plugin.RaidController.PendingFollowers.ToArray();
        if (pendingFollowers.Length == 0)
        {
            return;
        }

        plugin.LogPluginInfo($"Starting persisted follower raid spawn for {pendingFollowers.Length} followers");
        for (var index = 0; index < pendingFollowers.Length; index++)
        {
            var pendingFollower = pendingFollowers[index];
            try
            {
                var generatedProfile = await LoadPersistedProfileAsync(botCreator, localPlayer, pendingFollower, pendingFollowers.Length);
                await SynchronizationContextBridge.ResumeOnAsync(unityContext);
                var preloadCompleted = await BundlePreloadGuard.RunAsync(() => PreloadProfileBundlesAsync(generatedProfile.Profile));
                if (!preloadCompleted)
                {
                    plugin.LogPluginInfo($"Follower bundle preload canceled for {pendingFollower.Nickname}; continuing with spawn");
                }

                await SpawnFollowerAsync(
                    plugin,
                    botsController,
                    botSpawner,
                    botCreator,
                    localPlayer,
                    pendingFollower,
                    generatedProfile,
                    controlDecision,
                    index);
            }
            catch (Exception ex)
            {
                plugin.LogPluginError($"Failed to spawn persisted follower {pendingFollower.Nickname} ({pendingFollower.Aid})", ex);
            }
        }

        plugin.LogPluginInfo(
            $"Persisted follower raid spawn completed: spawned={pendingFollowers.Length - plugin.RaidController.PendingFollowers.Count}, pending={plugin.RaidController.PendingFollowers.Count}");
    }

    private static async Task SpawnFollowerAsync(
        FriendlyPmcCoreFollowersPlugin plugin,
        BotsController botsController,
        BotSpawner botSpawner,
        IBotCreator botCreator,
        Player localPlayer,
        FollowerSnapshotDto identitySnapshot,
        GeneratedFollowerProfile generatedProfile,
        DebugSpawnFollowerControlDecision controlDecision,
        int index)
    {
        var spawnPosition = GetSpawnPosition(localPlayer, index);
        var botZone = botsController.GetClosestZone(spawnPosition, out _)
            ?? throw new InvalidOperationException("Unable to find a bot zone near the player");
        var closestCorePoint = botsController.CoversData.GetClosest(spawnPosition).CorePointInGame
            ?? throw new InvalidOperationException("Unable to find a valid core point for follower spawn");
        var botCreationData = BotCreationDataClass.CreateWithoutProfile(generatedProfile.ProfileData);
        var profile = generatedProfile.Profile;

        botCreationData.AddPosition(spawnPosition, closestCorePoint.Id);
        botCreationData.AddProfile(profile);
        PrepareProfileIdentity(profile, localPlayer);
        plugin.Registry.MarkExpectedRuntimeProfileId(profile.ProfileId);

        plugin.LogPluginInfo(
            $"Prepared persisted follower profile {profile.Info.Nickname} (aid={identitySnapshot.Aid}, profileId={profile.ProfileId}, accountId={profile.AccountId})");

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
    }

    private static async Task<GeneratedFollowerProfile> LoadPersistedProfileAsync(
        IBotCreator botCreator,
        Player localPlayer,
        FollowerSnapshotDto snapshot,
        int followerCount)
    {
        var spawnParams = new BotSpawnParams
        {
            ShallBeGroup = new ShallBeGroupParams(true, false, Math.Max(2, followerCount + 1)),
        };

        var profileData = new BotProfileDataClass(localPlayer.Side, ResolveRole(localPlayer.Side), BotDifficulty.impossible, 0f, spawnParams, false);
        var conditions = profileData.PrepareToLoadBackend(1).ToList();
        var botPresets = BotPresetsField.GetValue(botCreator) as BotsPresets
            ?? throw new InvalidOperationException("Bot presets are unavailable");
        var profileEndpoint = SessionField.GetValue(botPresets) as ProfileEndpointFactoryAbstractClass
            ?? throw new InvalidOperationException("Profile endpoint is unavailable");

        var request = new LegacyParamsStruct
        {
            Url = profileEndpoint.Gclass1392_0.Main + "/client/game/bot/followergenerate",
            Params = new Dictionary<string, object>
            {
                ["Info"] = new Class19<List<WaveInfoClass>>(conditions),
                ["MemberId"] = snapshot.Aid,
            },
            Retries = LegacyParamsStruct.DefaultRetries,
        };

        var plugin = FriendlyPmcCoreFollowersPlugin.Instance
            ?? throw new InvalidOperationException("FriendlyPMC plugin instance is unavailable");

        plugin.LogPluginInfo(
            $"Requesting persisted follower descriptor: nickname={snapshot.Nickname}, aid={snapshot.Aid}, side={localPlayer.Side}, followerCount={followerCount}");

        var generatedProfiles = await profileEndpoint.method_3<CompleteProfileDescriptorClass[]>(request);
        var descriptor = generatedProfiles?.FirstOrDefault()
            ?? throw new InvalidOperationException($"Backend follower generation returned no profile for {snapshot.Aid}");

        LogDescriptorSummary(plugin, snapshot, descriptor);

        try
        {
            var profile = new Profile(descriptor);
            LogConstructedProfileInventoryDiff(plugin, snapshot, descriptor, profile);
            plugin.LogPluginInfo(
                $"Persisted follower descriptor constructed profile successfully: nickname={profile.Info.Nickname}, aid={snapshot.Aid}, profileId={profile.ProfileId}, accountId={profile.AccountId}, inventoryItems={profile.Inventory?.AllRealPlayerItems?.Count()}");
            return new GeneratedFollowerProfile(profile, profileData);
        }
        catch (Exception ex)
        {
            plugin.LogPluginError(
                $"Persisted follower descriptor construction failed for {snapshot.Nickname} ({snapshot.Aid})",
                ex);

            try
            {
                var dumpPath = DumpDescriptorToFile(snapshot, descriptor);
                plugin.LogPluginInfo(
                    $"Persisted follower descriptor dump written for {snapshot.Nickname} ({snapshot.Aid}): {dumpPath}");
            }
            catch (Exception dumpException)
            {
                plugin.LogPluginError(
                    $"Persisted follower descriptor dump failed for {snapshot.Nickname} ({snapshot.Aid})",
                    dumpException);
            }

            throw;
        }
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

    private static void LogDescriptorSummary(
        FriendlyPmcCoreFollowersPlugin plugin,
        FollowerSnapshotDto snapshot,
        CompleteProfileDescriptorClass descriptor)
    {
        var inventoryItems = descriptor.Inventory?.Gclass1390_0 ?? Array.Empty<FlatItemsDataClass>();
        var rootItems = inventoryItems.Count(item => item.parentId is null);
        var missingParents = inventoryItems.Count(item =>
            item.parentId is not null
            && !inventoryItems.Any(candidate => candidate._id == item.parentId.Value));
        var cartridgeLocations = inventoryItems
            .Where(item => string.Equals(item.slotId, "cartridges", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.location?.JToken?.Type.ToString() ?? "<null>")
            .ToArray();

        plugin.LogPluginInfo(
            $"Persisted follower descriptor summary: nickname={snapshot.Nickname}, aid={snapshot.Aid}, descriptorId={descriptor.Id}, accountId={descriptor.AccountId}, petId={(descriptor.PetId.HasValue ? descriptor.PetId.Value.ToString() : "<null>")}, traderCount={descriptor.TradersInfo?.Count ?? -1}, questCount={descriptor.QuestsData?.Count ?? -1}, inventoryItems={inventoryItems.Length}, rootItems={rootItems}, missingParents={missingParents}, cartridgeLocationKinds=[{string.Join(", ", cartridgeLocations)}]");
    }

    private static void LogConstructedProfileInventoryDiff(
        FriendlyPmcCoreFollowersPlugin plugin,
        FollowerSnapshotDto snapshot,
        CompleteProfileDescriptorClass descriptor,
        Profile profile)
    {
        try
        {
            var descriptorItems = descriptor.Inventory?.Gclass1390_0 ?? Array.Empty<FlatItemsDataClass>();
            var nonRootDescriptorItems = descriptorItems
                .Where(item => item.parentId is not null)
                .ToArray();
            var descriptorItemsById = nonRootDescriptorItems
                .GroupBy(item => item._id.ToString(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var realizedItems = profile.Inventory?.AllRealPlayerItems?.ToArray() ?? Array.Empty<Item>();
            var realizedItemIds = realizedItems
                .Select(item => item.Id.ToString())
                .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                .ToHashSet(StringComparer.Ordinal);
            var missingDescriptorItems = descriptorItemsById.Values
                .Where(item => !realizedItemIds.Contains(item._id.ToString()))
                .Take(6)
                .Select(item => $"{item._id}|tpl={item._tpl}|parent={item.parentId}|slot={item.slotId}")
                .ToArray();
            var extraRealizedItems = realizedItems
                .Where(item => !descriptorItemsById.ContainsKey(item.Id.ToString()))
                .Take(6)
                .Select(item => $"{item.Id}|tpl={item.TemplateId}|slot={item.CurrentAddress?.Container?.ID ?? "<null>"}")
                .ToArray();
            var missingDescriptorSummary = missingDescriptorItems.Length == 0
                ? "<none>"
                : string.Join(", ", missingDescriptorItems);
            var extraRealizedSummary = extraRealizedItems.Length == 0
                ? "<none>"
                : string.Join(", ", extraRealizedItems);

            plugin.LogPluginInfo(
                $"Persisted follower constructed profile inventory diff: nickname={snapshot.Nickname}, aid={snapshot.Aid}, descriptorNonRootItems={nonRootDescriptorItems.Length}, realizedItems={realizedItems.Length}, missingDescriptorItems=[{missingDescriptorSummary}], extraRealizedItems=[{extraRealizedSummary}]");
        }
        catch (Exception ex)
        {
            plugin.LogPluginInfo(
                $"Persisted follower constructed profile inventory diff failed: nickname={snapshot.Nickname}, aid={snapshot.Aid}, error={ex.GetType().Name}, detail={ex.Message}");
        }
    }

    private static string DumpDescriptorToFile(FollowerSnapshotDto snapshot, CompleteProfileDescriptorClass descriptor)
    {
        var dumpDirectory = Path.Combine(BepInEx.Paths.BepInExRootPath, "plugins", "FriendlyPMC.CoreFollowers", "payload-dumps");
        Directory.CreateDirectory(dumpDirectory);

        var safeName = string.Concat(snapshot.Nickname.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var fileName = $"persisted-follower-descriptor-{safeName}-{snapshot.Aid}.json";
        var fullPath = Path.Combine(dumpDirectory, fileName);

        var payload = new JObject
        {
            ["Nickname"] = snapshot.Nickname,
            ["Aid"] = snapshot.Aid,
            ["CapturedAtUtc"] = DateTime.UtcNow.ToString("O"),
            ["Descriptor"] = CreateSafeDescriptorDump(descriptor),
        };

        File.WriteAllText(fullPath, payload.ToString(Newtonsoft.Json.Formatting.Indented));
        return fullPath;
    }

    private static JObject CreateSafeDescriptorDump(CompleteProfileDescriptorClass descriptor)
    {
        var customization = new JObject();
        foreach (var entry in descriptor.Customization)
        {
            customization[entry.Key.ToString()] = entry.Value.ToString();
        }

        var items = new JArray(
            (descriptor.Inventory?.Gclass1390_0 ?? Array.Empty<FlatItemsDataClass>())
            .Select(item => new JObject
            {
                ["_id"] = item._id.ToString(),
                ["_tpl"] = item._tpl.ToString(),
                ["parentId"] = item.parentId?.ToString(),
                ["slotId"] = item.slotId,
                ["location"] = CloneWrappedToken(item.location),
                ["upd"] = CloneWrappedToken(item.upd),
            }));

        var info = new JObject
        {
            ["Nickname"] = descriptor.Info?.Nickname,
            ["MainProfileNickname"] = descriptor.Info?.MainProfileNickname,
            ["Side"] = descriptor.Info?.Side.ToString(),
            ["Level"] = descriptor.Info?.Level,
            ["Experience"] = descriptor.Info?.Experience,
            ["RegistrationDate"] = descriptor.Info?.RegistrationDate,
            ["SavageLockTime"] = descriptor.Info?.SavageLockTime,
            ["GroupId"] = descriptor.Info?.GroupId,
            ["TeamId"] = descriptor.Info?.TeamId,
            ["EntryPoint"] = descriptor.Info?.EntryPoint,
            ["GameVersion"] = descriptor.Info?.GameVersion,
            ["Type"] = descriptor.Info?.Type.ToString(),
            ["MemberCategory"] = descriptor.Info?.MemberCategory.ToString(),
            ["SelectedMemberCategory"] = descriptor.Info?.SelectedMemberCategory.ToString(),
        };

        return new JObject
        {
            ["Id"] = descriptor.Id.ToString(),
            ["AccountId"] = descriptor.AccountId,
            ["PetId"] = descriptor.PetId?.ToString(),
            ["KarmaValue"] = descriptor.KarmaValue,
            ["Info"] = info,
            ["Customization"] = customization,
            ["Inventory"] = new JObject
            {
                ["equipment"] = descriptor.Inventory?.MongoID_0.ToString(),
                ["stash"] = descriptor.Inventory?.Nullable_0?.ToString(),
                ["questRaidItems"] = descriptor.Inventory?.Nullable_1?.ToString(),
                ["questStashItems"] = descriptor.Inventory?.Nullable_2?.ToString(),
                ["sortingTable"] = descriptor.Inventory?.Nullable_3?.ToString(),
                ["hideoutCustomizationStashId"] = descriptor.Inventory?.Nullable_4?.ToString(),
                ["items"] = items,
            },
            ["Skills"] = new JObject
            {
                ["CommonCount"] = descriptor.Skills?.Common?.Length ?? 0,
                ["MasteringCount"] = descriptor.Skills?.Mastering?.Length ?? 0,
            },
            ["Stats"] = new JObject
            {
                ["Eft"] = new JObject
                {
                    ["SurvivorClass"] = descriptor.Stats?.Eft?.SurvivorClass.ToString(),
                    ["TotalInGameTime"] = descriptor.Stats?.Eft?.TotalInGameTime ?? 0,
                    ["DamageHistoryLethalPart"] = descriptor.Stats?.Eft?.DamageHistory?.LethalDamagePart.ToString(),
                }
            },
            ["Counts"] = new JObject
            {
                ["InsuredItems"] = descriptor.InsuredItems?.Length ?? 0,
                ["Quests"] = descriptor.QuestsData?.Count ?? 0,
                ["Achievements"] = descriptor.AchievementsData?.Count ?? 0,
                ["Prestige"] = descriptor.PrestigeData?.Count ?? 0,
                ["Variables"] = descriptor.VariableData?.Count ?? 0,
                ["Bonuses"] = descriptor.Bonuses?.Length ?? 0,
                ["WishList"] = descriptor.WishList?.Count ?? 0,
                ["CheckedMagazines"] = descriptor.CheckedMagazines?.Count ?? 0,
                ["CheckedChambers"] = descriptor.CheckedChambers?.Count ?? 0,
                ["TradersInfo"] = descriptor.TradersInfo?.Count ?? 0,
            },
        };
    }

    private static JToken? CloneWrappedToken(GClass846? wrapper)
    {
        return wrapper?.JToken?.DeepClone();
    }

    private static Vector3 GetSpawnPosition(Player localPlayer, int index)
    {
        var playerTransform = localPlayer.Transform;
        var row = index / 2;
        var side = index % 2 == 0 ? -1f : 1f;
        return playerTransform.position
            + (playerTransform.forward * (2.25f + (row * 0.9f)))
            + (playerTransform.right * side * (1.5f + (row * 0.6f)));
    }

    private static BotsGroup CreateFollowerGroup(
        FriendlyPmcCoreFollowersPlugin plugin,
        BotSpawner botSpawner,
        BotsController botsController,
        Player localPlayer,
        BotOwner owner,
        BotZone zone)
    {
        plugin.LogPluginInfo($"CreateFollowerGroup invoked for {owner.Profile.Info.Nickname}");
        PrepareOwnerIdentity(owner, localPlayer);

        var deadBodiesController = botSpawner.DeadBodiesController
            ?? throw new InvalidOperationException("Dead bodies controller is unavailable");
        var allPlayers = botSpawner.AllPlayers ?? new List<Player>();
        if (!allPlayers.Contains(localPlayer))
        {
            allPlayers.Add(localPlayer);
        }

        var activeEnemies = GetActiveEnemies(botSpawner, owner);
        var group = new PlayerFollowerBotsGroup(zone, botsController.BotGame, owner, activeEnemies, deadBodiesController, allPlayers, localPlayer);
        plugin.LogPluginInfo($"CreateFollowerGroup completed for {owner.Profile.Info.Nickname}");
        return group;
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
        PrepareFriendlyOwner(owner, localPlayer);
        plugin.LogPluginInfo($"Finalizing persisted follower spawn for {owner.Profile.Info.Nickname}");
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

    private static void BindFollower(
        FriendlyPmcCoreFollowersPlugin plugin,
        Player localPlayer,
        BotOwner spawnedOwner,
        FollowerSnapshotDto identitySnapshot,
        DebugSpawnFollowerControlDecision controlDecision)
    {
        var runtimeFollower = new BotOwnerFollowerRuntimeHandle(spawnedOwner, identitySnapshot);
        plugin.RegisterRuntimeFollower(runtimeFollower);
        plugin.RaidController.AttachSpawnedFollower(identitySnapshot);
        plugin.Registry.SetControlPathRuntime(
            runtimeFollower.Aid,
            CustomFollowerControlPathRuntime.Create(controlDecision.ControlPath, controlDecision.AbortReason));
        DebugSpawnFollowerSessionBinder.Bind(
            plugin.Registry,
            runtimeFollower.Aid,
            controlDecision.ControlPath);
        FollowerLootingRuntimeDisabler.DisableForFollower(spawnedOwner, plugin.LogPluginInfo);
        PrepareFriendlyOwner(spawnedOwner, localPlayer);
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
        plugin.LogPluginInfo($"Spawned persisted follower {spawnedOwner.Profile.Info.Nickname} (aid={runtimeFollower.Aid}, profileId={spawnedOwner.ProfileId})");
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
        }
        catch (NullReferenceException)
        {
            BindBotFollowerManually(owner, localPlayer, bossToFollow, botFollower, controlPath);
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

    private static void PrepareProfileIdentity(Profile profile, Player localPlayer)
    {
        if (profile.Info is { } profileInfo)
        {
            profileInfo.Side = localPlayer.Side;
            profileInfo.GroupId = localPlayer.GroupId;
            profileInfo.TeamId = localPlayer.Profile.Info.TeamId;
        }
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

    private readonly record struct GeneratedFollowerProfile(Profile Profile, BotProfileDataClass ProfileData);
}
#endif

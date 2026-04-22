using FriendlyPMC.Server.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonInfo = SPTarkov.Server.Core.Models.Eft.Common.Tables.Info;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerManagerSocialViewService(
    FollowerManagerService managerService,
    FollowerSquadManagerChatBot squadManagerChatBot,
    ProfileHelper? profileHelper = null,
    FollowerDiagnosticsLog? diagnosticsLog = null)
{
    private static readonly JsonSerializerOptions ProfileJsonSerializerOptions = CreateProfileJsonSerializerOptions();

    public async Task AppendRosterFriendsAsync(string sessionId, GetFriendListDataResponse friendList)
    {
        friendList.Friends ??= [];
        AppendFriend(friendList.Friends, squadManagerChatBot.GetChatBot());

        var managedProfiles = await managerService.LoadRosterProfilesForManagementAsync(sessionId);
        foreach (var profile in managedProfiles.OrderBy(member => member.Nickname, StringComparer.OrdinalIgnoreCase))
        {
            if (profile.Equipment is null)
            {
                continue;
            }

            AppendFriend(friendList.Friends, CreateFriendEntry(profile));
        }
    }

    public async Task<GetOtherProfileResponse?> TryBuildOtherProfileAsync(string sessionId, string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            diagnosticsLog?.Append($"social-profile session={sessionId} request=<blank> result=empty");
            return null;
        }

        var normalizedAccountId = accountId.Trim();
        var profile = await managerService.TryGetFollowerForManagementAsync(sessionId, normalizedAccountId);
        var visualizationInventory = profile is null ? null : ResolveVisualizationInventory(profile);
        if (visualizationInventory is null)
        {
            var roster = await managerService.LoadRosterProfilesForManagementAsync(sessionId);
            var socialAidMatch = roster.FirstOrDefault(candidate =>
                string.Equals(
                    BuildSocialAid(candidate.Aid).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    normalizedAccountId,
                    StringComparison.Ordinal));

            diagnosticsLog?.Append(
                $"social-profile session={sessionId} request={normalizedAccountId} result=miss socialAidMatch={(socialAidMatch?.Aid ?? "<none>")}");
            return null;
        }

        diagnosticsLog?.Append(
            $"social-profile session={sessionId} request={normalizedAccountId} result=hit aid={profile!.Aid} socialAid={BuildSocialAid(profile.Aid)} equipmentItems={visualizationInventory.Items.Count} favoriteItems=0");

        return CreateOtherProfileResponse(sessionId, profile!, visualizationInventory);
    }

    public async Task<bool> TryDeleteFollowerByFriendIdAsync(string sessionId, string friendId)
    {
        if (string.IsNullOrWhiteSpace(friendId))
        {
            return false;
        }

        if (string.Equals(friendId, squadManagerChatBot.GetChatBot().Id.ToString(), StringComparison.Ordinal))
        {
            return true;
        }

        var member = await managerService.TryGetRosterMemberAsync(sessionId, friendId);
        if (member is null)
        {
            return false;
        }

        await managerService.DeleteFollowerAsync(sessionId, member.Aid);
        return true;
    }

    private static void AppendFriend(ICollection<UserDialogInfo> friends, UserDialogInfo candidate)
    {
        if (friends.Any(existing => string.Equals(existing.Id.ToString(), candidate.Id.ToString(), StringComparison.Ordinal)))
        {
            return;
        }

        friends.Add(candidate);
    }

    private static UserDialogInfo CreateFriendEntry(FollowerProfileSnapshot profile)
    {
        return new UserDialogInfo
        {
            Id = new MongoId(profile.Aid),
            Aid = BuildSocialAid(profile.Aid),
            Info = new UserDialogDetails
            {
                Nickname = profile.Nickname,
                Side = profile.Side,
                Level = Math.Max(profile.Level, 1),
                MemberCategory = MemberCategory.Group,
                SelectedMemberCategory = MemberCategory.Group,
            },
        };
    }

    private GetOtherProfileResponse CreateOtherProfileResponse(
        string sessionId,
        FollowerProfileSnapshot profile,
        FollowerInventorySnapshot visualizationInventory)
    {
        var equipment = visualizationInventory.ToEquipmentSnapshot();
        var appearance = ResolveVisualizationAppearance(sessionId, profile, equipment);
        var inventoryItems = equipment.Items.Select(FollowerProfileFactory.CreateInventoryItem).ToList();
        var playerBaseline = LoadPlayerBaseline(sessionId);

        return new GetOtherProfileResponse
        {
            Id = new MongoId(profile.Aid),
            Aid = BuildSocialAid(profile.Aid),
            Info = new OtherProfileInfo
            {
                Nickname = profile.Nickname,
                Side = profile.Side,
                Experience = profile.Experience,
                MemberCategory = (int)MemberCategory.Group,
                BannedState = false,
                BannedUntil = 0,
                RegistrationDate = playerBaseline.PmcInfo?.RegistrationDate ?? 0,
            },
            Customization = new OtherProfileCustomization
            {
                Head = appearance?.Head ?? string.Empty,
                Body = appearance?.Body ?? string.Empty,
                Feet = appearance?.Feet ?? string.Empty,
                Hands = appearance?.Hands ?? string.Empty,
                Voice = appearance?.Voice ?? string.Empty,
                Dogtag = appearance?.DogTag ?? string.Empty,
            },
            Skills = BuildSkills(profile.SkillProgress),
            Equipment = new OtherProfileEquipment
            {
                Id = equipment.EquipmentId,
                Items = inventoryItems,
            },
            Achievements = playerBaseline.Achievements,
            FavoriteItems = playerBaseline.FavoriteItems,
            PmcStats = CreateOtherProfileStats(playerBaseline.PmcStats),
            ScavStats = CreateOtherProfileStats(playerBaseline.ScavStats),
            Hideout = playerBaseline.Hideout,
            CustomizationStash = playerBaseline.CustomizationStash,
            HideoutAreaStashes = playerBaseline.HideoutAreaStashes,
            Items = playerBaseline.HideoutItems,
        };
    }

    private static FollowerInventorySnapshot? ResolveVisualizationInventory(FollowerProfileSnapshot profile)
    {
        return profile.Inventory ?? FollowerInventoryMigrationPolicy.CreateInventorySnapshot(profile.Equipment);
    }

    private PlayerSocialProfileBaseline LoadPlayerBaseline(string sessionId)
    {
        var baselineFromHelper = TryLoadPlayerBaselineFromProfileHelper(sessionId);
        if (baselineFromHelper is not null)
        {
            return baselineFromHelper;
        }

        return TryLoadPlayerBaselineFromProfileFile(sessionId) ?? PlayerSocialProfileBaseline.Empty;
    }

    private PlayerSocialProfileBaseline? TryLoadPlayerBaselineFromProfileHelper(string sessionId)
    {
        if (profileHelper is null)
        {
            return null;
        }

        var sessionMongoId = new MongoId(sessionId);
        var pmcProfile = profileHelper.GetPmcProfile(sessionMongoId);
        if (pmcProfile is null)
        {
            return null;
        }

        var scavProfile = profileHelper.GetScavProfile(sessionMongoId);
        return new PlayerSocialProfileBaseline(
            pmcProfile.Info,
            pmcProfile.Stats,
            scavProfile?.Stats,
            pmcProfile.Hideout ?? CreateEmptyHideout(),
            pmcProfile.Achievements ?? new Dictionary<MongoId, long>(),
            profileHelper.GetOtherProfileFavorites(pmcProfile) ?? [],
            pmcProfile.Inventory?.HideoutCustomizationStashId?.ToString() ?? string.Empty,
            pmcProfile.Inventory?.HideoutAreaStashes?.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, MongoId>(StringComparer.Ordinal),
            CollectHideoutItems(pmcProfile.Inventory));
    }

    private static PlayerSocialProfileBaseline? TryLoadPlayerBaselineFromProfileFile(string sessionId)
    {
        var profilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "user", "profiles", $"{sessionId}.json");
        if (!File.Exists(profilePath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(profilePath));
        if (!document.RootElement.TryGetProperty("characters", out var characters)
            || !characters.TryGetProperty("pmc", out var pmc))
        {
            return null;
        }

        var pmcInfo = DeserializeProfileSection<CommonInfo>(pmc, "Info");
        var pmcStats = DeserializeProfileSection<Stats>(pmc, "Stats");
        var hideout = DeserializeHideoutSection(pmc, "Hideout");
        var achievements = DeserializeProfileSection<Dictionary<MongoId, long>>(pmc, "Achievements") ?? new Dictionary<MongoId, long>();
        var inventory = DeserializeProfileSection<BotBaseInventory>(pmc, "Inventory");
        Stats? scavStats = null;
        if (characters.TryGetProperty("scav", out var scav))
        {
            scavStats = DeserializeProfileSection<Stats>(scav, "Stats");
        }

        return new PlayerSocialProfileBaseline(
            pmcInfo,
            pmcStats,
            scavStats,
            hideout,
            achievements,
            [],
            inventory?.HideoutCustomizationStashId?.ToString() ?? string.Empty,
            inventory?.HideoutAreaStashes?.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, MongoId>(StringComparer.Ordinal),
            CollectHideoutItems(inventory));
    }

    private static FollowerAppearanceSnapshot? ResolveVisualizationAppearance(
        string sessionId,
        FollowerProfileSnapshot profile,
        FollowerEquipmentSnapshot equipment)
    {
        var fallbackAppearance = profile.Appearance;
        if (!HasAppearanceValues(fallbackAppearance))
        {
            fallbackAppearance = TryLoadPlayerCustomizationFallback(sessionId);
        }

        if (!HasAppearanceValues(fallbackAppearance))
        {
            return fallbackAppearance;
        }

        return fallbackAppearance! with
        {
            DogTag = ResolveDogTagVisualizationId(equipment, fallbackAppearance!.DogTag),
        };
    }

    private static bool HasAppearanceValues(FollowerAppearanceSnapshot? appearance)
    {
        return appearance is not null
            && (!string.IsNullOrWhiteSpace(appearance.Head)
                || !string.IsNullOrWhiteSpace(appearance.Body)
                || !string.IsNullOrWhiteSpace(appearance.Feet)
                || !string.IsNullOrWhiteSpace(appearance.Hands)
                || !string.IsNullOrWhiteSpace(appearance.Voice)
                || !string.IsNullOrWhiteSpace(appearance.DogTag));
    }

    private static string? ResolveDogTagVisualizationId(FollowerEquipmentSnapshot equipment, string? fallbackDogTag)
    {
        var dogTagItemId = equipment.Items.FirstOrDefault(item =>
            string.Equals(item.SlotId, "Dogtag", StringComparison.OrdinalIgnoreCase))?.Id;
        return string.IsNullOrWhiteSpace(dogTagItemId)
            ? fallbackDogTag
            : dogTagItemId;
    }

    private static FollowerAppearanceSnapshot? TryLoadPlayerCustomizationFallback(string sessionId)
    {
        var profilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "user", "profiles", $"{sessionId}.json");
        if (!System.IO.File.Exists(profilePath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(profilePath));
        if (!document.RootElement.TryGetProperty("characters", out var characters)
            || !characters.TryGetProperty("pmc", out var pmc)
            || !pmc.TryGetProperty("Customization", out var customization))
        {
            return null;
        }

        return new FollowerAppearanceSnapshot(
            ReadCustomizationValue(customization, "Head"),
            ReadCustomizationValue(customization, "Body"),
            ReadCustomizationValue(customization, "Feet"),
            ReadCustomizationValue(customization, "Hands"),
            ReadCustomizationValue(customization, "Voice"),
            ReadCustomizationValue(customization, "DogTag"));
    }

    private static string? ReadCustomizationValue(JsonElement customization, string propertyName)
    {
        return customization.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int BuildSocialAid(string followerAid)
    {
        const uint fnvOffsetBasis = 2166136261;
        const uint fnvPrime = 16777619;

        var hash = fnvOffsetBasis;
        foreach (var ch in followerAid)
        {
            hash ^= ch;
            hash *= fnvPrime;
        }

        var socialAid = (int)(hash & 0x7FFFFFFF);
        return socialAid == 0 ? 1 : socialAid;
    }

    private static Skills BuildSkills(IReadOnlyDictionary<string, int> skillProgress)
    {
        var commonSkills = new List<CommonSkill>();
        foreach (var skill in skillProgress)
        {
            if (!Enum.TryParse<SkillTypes>(skill.Key, out var skillType))
            {
                continue;
            }

            commonSkills.Add(new CommonSkill
            {
                Id = skillType,
                Progress = skill.Value,
            });
        }

        return new Skills
        {
            Common = commonSkills,
            Mastering = Array.Empty<MasterySkill>(),
            Points = 0d,
        };
    }

    private static OtherProfileStats CreateOtherProfileStats(Stats? stats)
    {
        return new OtherProfileStats
        {
            Eft = new OtherProfileSubStats
            {
                TotalInGameTime = stats?.Eft?.TotalInGameTime ?? 0,
                OverAllCounters = stats?.Eft?.OverallCounters ?? new OverallCounters
                {
                    Items = [],
                },
            },
        };
    }

    private static Hideout CreateEmptyHideout()
    {
        return new Hideout
        {
            Production = new Dictionary<MongoId, Production?>(),
            Areas = [],
            Improvements = new Dictionary<MongoId, HideoutImprovement>(),
            Seed = string.Empty,
            Customization = new Dictionary<string, MongoId>(StringComparer.Ordinal),
            MannequinPoses = new Dictionary<MongoId, MongoId>(),
        };
    }

    private static JsonSerializerOptions CreateProfileJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new MongoIdJsonConverter());
        return options;
    }

    private static TSection? DeserializeProfileSection<TSection>(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
        {
            return default;
        }

        return element.Deserialize<TSection>(ProfileJsonSerializerOptions);
    }

    private static Hideout DeserializeHideoutSection(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var hideoutElement))
        {
            return CreateEmptyHideout();
        }

        return new Hideout
        {
            Production = new Dictionary<MongoId, Production?>(),
            Areas = DeserializeProfileSection<List<BotHideoutArea>>(hideoutElement, "Areas") ?? [],
            Improvements = new Dictionary<MongoId, HideoutImprovement>(),
            Seed = hideoutElement.TryGetProperty("Seed", out var seedElement) && seedElement.ValueKind == JsonValueKind.String
                ? seedElement.GetString() ?? string.Empty
                : string.Empty,
            Customization = DeserializeProfileSection<Dictionary<string, MongoId>>(hideoutElement, "Customization")
                ?? new Dictionary<string, MongoId>(StringComparer.Ordinal),
            MannequinPoses = DeserializeProfileSection<Dictionary<MongoId, MongoId>>(hideoutElement, "MannequinPoses")
                ?? new Dictionary<MongoId, MongoId>(),
        };
    }

    private static List<Item> CollectHideoutItems(BotBaseInventory? inventory)
    {
        if (inventory?.Items is null || inventory.Items.Count == 0)
        {
            return [];
        }

        var rootIds = new HashSet<string>(StringComparer.Ordinal);
        if (inventory.HideoutCustomizationStashId is MongoId customizationStashId)
        {
            rootIds.Add(customizationStashId.ToString());
        }

        if (inventory.HideoutAreaStashes is not null)
        {
            foreach (var stashId in inventory.HideoutAreaStashes.Values)
            {
                rootIds.Add(stashId.ToString());
            }
        }

        if (rootIds.Count == 0)
        {
            return [];
        }

        var itemsByParentId = inventory.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var includedIds = new HashSet<string>(rootIds, StringComparer.Ordinal);
        var pendingIds = new Queue<string>(rootIds);
        while (pendingIds.Count > 0)
        {
            var parentId = pendingIds.Dequeue();
            if (!itemsByParentId.TryGetValue(parentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                var childId = child.Id.ToString();
                if (!includedIds.Add(childId))
                {
                    continue;
                }

                pendingIds.Enqueue(childId);
            }
        }

        return inventory.Items
            .Where(item => includedIds.Contains(item.Id.ToString()))
            .ToList();
    }

    private sealed record PlayerSocialProfileBaseline(
        CommonInfo? PmcInfo,
        Stats? PmcStats,
        Stats? ScavStats,
        Hideout Hideout,
        Dictionary<MongoId, long> Achievements,
        List<Item> FavoriteItems,
        string CustomizationStash,
        Dictionary<string, MongoId> HideoutAreaStashes,
        List<Item> HideoutItems)
    {
        public static PlayerSocialProfileBaseline Empty { get; } = new(
            null,
            null,
            null,
            CreateEmptyHideout(),
            new Dictionary<MongoId, long>(),
            [],
            string.Empty,
            new Dictionary<string, MongoId>(StringComparer.Ordinal),
            []);
    }

    private sealed class MongoIdJsonConverter : JsonConverter<MongoId>
    {
        public override MongoId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new MongoId(reader.GetString() ?? string.Empty);
        }

        public override MongoId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new MongoId(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(value.ToString());
        }
    }
}

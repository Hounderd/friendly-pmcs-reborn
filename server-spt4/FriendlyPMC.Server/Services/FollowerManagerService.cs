using FriendlyPMC.Server.Models;
using FriendlyPMC.Server.Models.Responses;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using System.Reflection;
using System.Text.Json;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerManagerService
{
    private readonly FollowerRosterStore store;
    private readonly FollowerProfileFactory profileFactory;
    private readonly ProfileHelper? profileHelper;
    private readonly FollowerDiagnosticsLog? diagnosticsLog;

    public FollowerManagerService(
        FollowerRosterStore store,
        FollowerProfileFactory profileFactory,
        ProfileHelper? profileHelper = null,
        FollowerDiagnosticsLog? diagnosticsLog = null)
    {
        this.store = store;
        this.profileFactory = profileFactory;
        this.profileHelper = profileHelper;
        this.diagnosticsLog = diagnosticsLog;
    }

    public Task<IReadOnlyList<FollowerRosterRecord>> LoadRosterAsync(string sessionId)
    {
        return LoadRosterForRequestedSessionAsync(sessionId);
    }

    public Task<IReadOnlyList<FollowerProfileSnapshot>> LoadStoredProfilesAsync(string sessionId)
    {
        return LoadProfilesForRequestedSessionAsync(sessionId);
    }

    public async Task<IReadOnlyList<FollowerProfileSnapshot>> LoadActiveFollowersForRaidAsync(string sessionId)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var profilesByAid = profiles
            .ToDictionary(profile => profile.Aid, StringComparer.Ordinal);

        var followers = roster
            .Where(member => member.AutoJoin)
            .Select(member => BuildResolvedProfile(member, profilesByAid.GetValueOrDefault(member.Aid)))
            .ToArray();

        diagnosticsLog?.Append(
            $"active-load requested={sessionId} resolved={resolvedSessionId} roster={roster.Count} autojoin={roster.Count(member => member.AutoJoin)} profiles={profilesByAid.Count} returned={followers.Length}");

        return followers;
    }

    public async Task<IReadOnlyList<FollowerProfileSnapshot>> LoadRosterProfilesForManagementAsync(string sessionId)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var profilesByAid = profiles
            .ToDictionary(profile => profile.Aid, StringComparer.Ordinal);

        return roster
            .Select(member => BuildManagedProfile(member, profilesByAid.GetValueOrDefault(member.Aid)))
            .ToArray();
    }

    public async Task<FollowerProfileSnapshot?> TryGetFollowerForRaidAsync(string sessionId, string aid)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var member = roster.FirstOrDefault(existing => string.Equals(existing.Aid, aid, StringComparison.Ordinal));
        if (member is null)
        {
            return null;
        }

        var profile = profiles.FirstOrDefault(existing => string.Equals(existing.Aid, aid, StringComparison.Ordinal));

        return BuildResolvedProfile(member, profile);
    }

    public async Task<FollowerProfileSnapshot?> TryGetFollowerForManagementAsync(string sessionId, string aid)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var member = roster.FirstOrDefault(existing => string.Equals(existing.Aid, aid, StringComparison.Ordinal));
        if (member is null)
        {
            return null;
        }

        var profile = profiles.FirstOrDefault(existing => string.Equals(existing.Aid, aid, StringComparison.Ordinal));

        return BuildManagedProfile(member, profile);
    }

    public async Task<IReadOnlyList<FollowerManagerMemberDto>> GetRosterViewAsync(string sessionId)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var profilesByAid = profiles
            .ToDictionary(profile => profile.Aid, StringComparer.Ordinal);

        diagnosticsLog?.Append(
            $"roster-view requested={sessionId} resolved={resolvedSessionId} roster={roster.Count} profiles={profilesByAid.Count}");

        return roster
            .Select(member =>
            {
                var resolved = BuildResolvedProfile(member, profilesByAid.GetValueOrDefault(member.Aid));
                return new FollowerManagerMemberDto(
                    member.Aid,
                    member.Nickname,
                    member.Side,
                    member.AutoJoin,
                    member.LoadoutMode,
                    member.AssignedEquipmentBuildName,
                    resolved.Level,
                    resolved.Experience,
                    profilesByAid.ContainsKey(member.Aid));
            })
            .OrderBy(member => member.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<FollowerManagerMemberDto> AddFollowerAsync(string sessionId, string? nickname)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);

        var resolvedNickname = ResolveNickname(roster, nickname);
        EnsureNicknameAvailable(roster, resolvedNickname, aidToIgnore: null);

        var side = ResolveDefaultSide(sessionId);
        var (seedLevel, seedExperience) = ResolveDefaultProfileSeed(sessionId);
        var record = new FollowerRosterRecord(
            new MongoId().ToString(),
            resolvedNickname,
            side,
            AutoJoin: true,
            LoadoutMode: FollowerLoadoutModes.Persisted);
        roster.Add(record);
        profiles.Add(profileFactory.CreateEmpty(record, seedLevel, seedExperience));

        await SaveNormalizedStateAsync(resolvedSessionId, roster, profiles);

        return new FollowerManagerMemberDto(record.Aid, record.Nickname, record.Side, record.AutoJoin, record.LoadoutMode, record.AssignedEquipmentBuildName, seedLevel, seedExperience, true);
    }

    public async Task<FollowerManagerMemberDto> RenameFollowerAsync(string sessionId, string aid, string nickname)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);

        var member = FindRosterMember(roster, aid);
        EnsureNicknameAvailable(roster, nickname, member.Aid);

        var updatedMember = member with { Nickname = nickname.Trim() };
        ReplaceRosterMember(roster, updatedMember);
        ReplaceProfile(profiles, updatedMember.Aid, profile => profile with { Nickname = updatedMember.Nickname });

        await SaveNormalizedStateAsync(resolvedSessionId, roster, profiles);

        var resolved = BuildResolvedProfile(updatedMember, profiles.FirstOrDefault(profile => profile.Aid == updatedMember.Aid));
        return new FollowerManagerMemberDto(
            updatedMember.Aid,
            updatedMember.Nickname,
            updatedMember.Side,
            updatedMember.AutoJoin,
            updatedMember.LoadoutMode,
            updatedMember.AssignedEquipmentBuildName,
            resolved.Level,
            resolved.Experience,
            profiles.Any(profile => profile.Aid == updatedMember.Aid));
    }

    public async Task DeleteFollowerAsync(string sessionId, string aid)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (rosterState, profileState) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var roster = rosterState
            .Where(member => !string.Equals(member.Aid, aid, StringComparison.Ordinal))
            .ToArray();
        var profiles = profileState
            .Where(profile => !string.Equals(profile.Aid, aid, StringComparison.Ordinal))
            .ToArray();

        await SaveNormalizedStateAsync(resolvedSessionId, roster, profiles);
    }

    public async Task<FollowerManagerMemberDto> SetAutoJoinAsync(string sessionId, string aid, bool autoJoin)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var member = FindRosterMember(roster, aid) with { AutoJoin = autoJoin };
        ReplaceRosterMember(roster, member);

        await SaveNormalizedStateAsync(resolvedSessionId, roster, profiles);
        var resolved = BuildResolvedProfile(member, profiles.FirstOrDefault(profile => profile.Aid == member.Aid));
        return new FollowerManagerMemberDto(
            member.Aid,
            member.Nickname,
            member.Side,
            member.AutoJoin,
            member.LoadoutMode,
            member.AssignedEquipmentBuildName,
            resolved.Level,
            resolved.Experience,
            profiles.Any(profile => profile.Aid == member.Aid));
    }

    public async Task<FollowerManagerMemberDto> SetLoadoutModeAsync(string sessionId, string aid, string loadoutMode)
    {
        var normalizedMode = NormalizeLoadoutMode(loadoutMode);
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var member = FindRosterMember(roster, aid) with { LoadoutMode = normalizedMode };
        ReplaceRosterMember(roster, member);

        await SaveNormalizedStateAsync(resolvedSessionId, roster, profiles);
        var resolved = BuildResolvedProfile(member, profiles.FirstOrDefault(profile => profile.Aid == member.Aid));
        return new FollowerManagerMemberDto(
            member.Aid,
            member.Nickname,
            member.Side,
            member.AutoJoin,
            member.LoadoutMode,
            member.AssignedEquipmentBuildName,
            resolved.Level,
            resolved.Experience,
            profiles.Any(profile => profile.Aid == member.Aid));
    }

    public IReadOnlyList<string> GetAvailableEquipmentBuildNames(string sessionId)
    {
        var (fullProfile, source) = ResolveEquipmentBuildProfile(sessionId);
        var buildNames = FollowerEquipmentBuildReflectionPolicy.ResolveBuildNames(fullProfile);
        diagnosticsLog?.Append($"equiplist session={sessionId} helper={(profileHelper is not null)} source={source} count={buildNames.Count}");
        return buildNames;
    }

    public async Task<FollowerManagerMemberDto> ApplyEquipmentBuildAsync(string sessionId, string aid, string buildName)
    {
        if (profileHelper is null)
        {
            throw new InvalidOperationException("Equipment build management is unavailable because ProfileHelper is not registered.");
        }

        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var member = FindRosterMember(roster, aid);
        var (fullProfile, source) = ResolveEquipmentBuildProfile(sessionId);
        if (!FollowerEquipmentBuildReflectionPolicy.TryCreateEquipmentSnapshot(fullProfile, buildName, out var equipmentSnapshot))
        {
            diagnosticsLog?.Append($"equip-apply session={sessionId} aid={aid} build={buildName.Trim()} helper={(profileHelper is not null)} source={source} result=missing");
            throw new InvalidOperationException($"Equipment build '{buildName.Trim()}' was not found.");
        }

        var updatedMember = member with
        {
            LoadoutMode = FollowerLoadoutModes.Persisted,
            AssignedEquipmentBuildName = buildName.Trim(),
        };
        ReplaceRosterMember(roster, updatedMember);

        var existingProfile = profiles.FirstOrDefault(profile => string.Equals(profile.Aid, aid, StringComparison.Ordinal))
            ?? profileFactory.CreateEmpty(updatedMember);
        var updatedProfile = existingProfile with
        {
            Nickname = updatedMember.Nickname,
            Side = updatedMember.Side,
            Equipment = equipmentSnapshot,
        };
        ReplaceOrAddProfile(profiles, updatedProfile);

        await SaveNormalizedStateAsync(resolvedSessionId, roster, profiles);
        diagnosticsLog?.Append($"equip-apply session={sessionId} aid={aid} build={buildName.Trim()} helper={(profileHelper is not null)} source={source} result=applied items={equipmentSnapshot.Items.Count}");

        return new FollowerManagerMemberDto(
            updatedMember.Aid,
            updatedMember.Nickname,
            updatedMember.Side,
            updatedMember.AutoJoin,
            updatedMember.LoadoutMode,
            updatedMember.AssignedEquipmentBuildName,
            updatedProfile.Level,
            updatedProfile.Experience,
            true);
    }

    public async Task<FollowerManagerMemberDto?> TryGetRosterMemberAsync(string sessionId, string aid)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var member = roster.FirstOrDefault(existing => string.Equals(existing.Aid, aid, StringComparison.Ordinal));
        if (member is null)
        {
            return null;
        }

        var resolved = BuildResolvedProfile(member, profiles.FirstOrDefault(profile => profile.Aid == member.Aid));
        return new FollowerManagerMemberDto(
            member.Aid,
            member.Nickname,
            member.Side,
            member.AutoJoin,
            member.LoadoutMode,
            member.AssignedEquipmentBuildName,
            resolved.Level,
            resolved.Experience,
            profiles.Any(profile => profile.Aid == member.Aid));
    }

    public async Task<FollowerManagerMemberDto?> TryGetRosterMemberByNicknameAsync(string sessionId, string nickname)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, _) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var member = roster.FirstOrDefault(existing => string.Equals(existing.Nickname, nickname.Trim(), StringComparison.OrdinalIgnoreCase));
        if (member is null)
        {
            return null;
        }

        return await TryGetRosterMemberAsync(resolvedSessionId, member.Aid);
    }

    public async Task RegisterRecruitAsync(string sessionId, FollowerRosterRecord follower)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, profiles) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);

        if (roster.All(existing => existing.Aid != follower.Aid))
        {
            if (roster.Any(existing => string.Equals(existing.Nickname, follower.Nickname, StringComparison.OrdinalIgnoreCase)))
            {
                diagnosticsLog?.Append($"recruit-skip-duplicate session={sessionId} aid={follower.Aid} nickname={follower.Nickname}");
                return;
            }

            var normalized = follower with
            {
                AutoJoin = true,
                LoadoutMode = NormalizeLoadoutMode(follower.LoadoutMode),
            };

            roster.Add(normalized);
            if (profiles.All(existing => existing.Aid != normalized.Aid))
            {
                profiles.Add(profileFactory.CreateEmpty(normalized));
            }

            await SaveNormalizedStateAsync(resolvedSessionId, roster, profiles);
        }
    }

    public async Task SaveRaidProgressAsync(
        string sessionId,
        IReadOnlyList<FollowerProfileSnapshot> followers,
        IReadOnlyList<string>? raidStartFollowerAids = null,
        IReadOnlyList<string>? spawnedFollowerAids = null,
        IReadOnlyList<string>? deadFollowerAids = null)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        var (roster, existingProfilesList) = await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true);
        var incomingByAid = followers.ToDictionary(profile => profile.Aid, StringComparer.Ordinal);
        var raidStartSet = new HashSet<string>(raidStartFollowerAids ?? Array.Empty<string>(), StringComparer.Ordinal);
        var spawnedSet = new HashSet<string>(spawnedFollowerAids ?? Array.Empty<string>(), StringComparer.Ordinal);
        var deadSet = new HashSet<string>(deadFollowerAids ?? Array.Empty<string>(), StringComparer.Ordinal);
        var existingProfiles = existingProfilesList
            .ToDictionary(profile => profile.Aid, StringComparer.Ordinal);

        var rosterByAid = roster.ToDictionary(member => member.Aid, StringComparer.Ordinal);
        var profilesToPersist = new List<FollowerProfileSnapshot>(incomingByAid.Values.Count);
        foreach (var incoming in incomingByAid.Values)
        {
            if (deadSet.Contains(incoming.Aid))
            {
                continue;
            }

            if (!rosterByAid.TryGetValue(incoming.Aid, out var member))
            {
                continue;
            }

            profilesToPersist.Add(BuildSavedRaidProfile(member, incoming));
        }

        foreach (var existing in existingProfiles.Values)
        {
            if (incomingByAid.ContainsKey(existing.Aid))
            {
                continue;
            }

            if (deadSet.Contains(existing.Aid))
            {
                continue;
            }

            if (!rosterByAid.TryGetValue(existing.Aid, out var member))
            {
                continue;
            }

            profilesToPersist.Add(BuildResolvedProfile(member, existing));
        }

        var rosterToPersist = roster
            .Where(member => !deadSet.Contains(member.Aid))
            .ToArray();

        diagnosticsLog?.Append(
            $"raid-save requested={sessionId} resolved={resolvedSessionId} incoming={followers.Count} raidStart={raidStartSet.Count} spawned={spawnedSet.Count} dead={deadSet.Count} rosterBefore={roster.Count} rosterAfter={rosterToPersist.Length} profilesBefore={existingProfiles.Count} profilesAfter={profilesToPersist.Count}");

        await SaveNormalizedStateAsync(resolvedSessionId, rosterToPersist, profilesToPersist);
    }

    private async Task<IReadOnlyList<FollowerRosterRecord>> LoadRosterForRequestedSessionAsync(string sessionId)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        return (await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true)).Roster;
    }

    private async Task<IReadOnlyList<FollowerProfileSnapshot>> LoadProfilesForRequestedSessionAsync(string sessionId)
    {
        var resolvedSessionId = await ResolveStorageSessionIdAsync(sessionId);
        return (await LoadNormalizedStateAsync(resolvedSessionId, persistNormalized: true)).Profiles;
    }

    private async Task<string> ResolveStorageSessionIdAsync(string sessionId)
    {
        var roster = await store.LoadRosterAsync(sessionId);
        if (roster.Count > 0)
        {
            return sessionId;
        }

        var knownSessionIds = store.GetKnownSessionIds();
        if (knownSessionIds.Count != 1)
        {
            return sessionId;
        }

        var fallbackSessionId = knownSessionIds[0];
        if (string.Equals(fallbackSessionId, sessionId, StringComparison.Ordinal))
        {
            return sessionId;
        }

        var fallbackRoster = await store.LoadRosterAsync(fallbackSessionId);
        if (fallbackRoster.Count == 0)
        {
            return sessionId;
        }

        diagnosticsLog?.Append($"session-fallback requested={sessionId} resolved={fallbackSessionId} roster={fallbackRoster.Count}");
        return fallbackSessionId;
    }

    private FollowerProfileSnapshot BuildResolvedProfile(FollowerRosterRecord member, FollowerProfileSnapshot? profile)
    {
        var resolved = BuildManagedProfile(member, profile);

        if (string.Equals(member.LoadoutMode, FollowerLoadoutModes.Generated, StringComparison.OrdinalIgnoreCase))
        {
            return resolved with
            {
                Equipment = null,
            };
        }

        return resolved;
    }

    private FollowerProfileSnapshot BuildSavedRaidProfile(
        FollowerRosterRecord member,
        FollowerProfileSnapshot incoming)
    {
        return BuildResolvedProfile(member, incoming);
    }

    private FollowerProfileSnapshot BuildManagedProfile(FollowerRosterRecord member, FollowerProfileSnapshot? profile)
    {
        return profile is null
            ? profileFactory.CreateEmpty(member)
            : profile with
            {
                Nickname = member.Nickname,
                Side = member.Side,
            };
    }

    private string ResolveDefaultSide(string sessionId)
    {
        return profileHelper?.GetPmcProfile(new MongoId(sessionId))?.Info?.Side
            ?? "Usec";
    }

    private (int Level, int Experience) ResolveDefaultProfileSeed(string sessionId)
    {
        var profile = profileHelper?.GetPmcProfile(new MongoId(sessionId));
        if (profile is null)
        {
            return (0, 0);
        }

        var level = Math.Max(profile.Info?.Level ?? 0, 0);
        var experience = Math.Max(
            ReadIntProperty(profile, "Experience")
            ?? ReadIntProperty(profile.Info, "Experience")
            ?? 0,
            0);

        return (level, experience);
    }

    private static int? ReadIntProperty(object? target, string propertyName)
    {
        if (target is null)
        {
            return null;
        }

        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(target) is null)
        {
            return null;
        }

        var value = property.GetValue(target);
        return value switch
        {
            int intValue => intValue,
            long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
            _ => null,
        };
    }

    private object? ResolveFullProfile(string sessionId)
    {
        var helperProfile = ResolveFullProfileFromHelper(sessionId);
        if (helperProfile is not null)
        {
            return helperProfile;
        }

        return ResolveFullProfileFromDisk(sessionId);
    }

    private (object? Profile, string Source) ResolveEquipmentBuildProfile(string sessionId)
    {
        var helperProfile = ResolveFullProfileFromHelper(sessionId);
        var helperBuilds = FollowerEquipmentBuildReflectionPolicy.ResolveBuildNames(helperProfile);
        if (helperBuilds.Count > 0)
        {
            return (helperProfile, "helper");
        }

        var diskProfile = ResolveFullProfileFromDisk(sessionId);
        var diskBuilds = FollowerEquipmentBuildReflectionPolicy.ResolveBuildNames(diskProfile);
        if (diskBuilds.Count > 0)
        {
            return (diskProfile, helperProfile is null ? "disk" : "disk-fallback");
        }

        if (helperProfile is not null)
        {
            return (helperProfile, "helper-empty");
        }

        return (diskProfile, diskProfile is not null ? "disk-empty" : "none");
    }

    private object? ResolveFullProfileFromHelper(string sessionId)
    {
        if (profileHelper is null)
        {
            return null;
        }

        var helperType = profileHelper.GetType();
        var method = helperType.GetMethod("GetFullProfile", [typeof(MongoId)]);
        if (method is not null)
        {
            return method.Invoke(profileHelper, [new MongoId(sessionId)]);
        }

        method = helperType.GetMethod("GetFullProfile", [typeof(string)]);
        return method?.Invoke(profileHelper, [sessionId]);
    }

    private static JsonElement? ResolveFullProfileFromDisk(string sessionId)
    {
        var profilePath = Path.Combine(AppContext.BaseDirectory, "user", "profiles", $"{sessionId}.json");
        if (!File.Exists(profilePath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(profilePath));
        return document.RootElement.Clone();
    }

    private async Task<(List<FollowerRosterRecord> Roster, List<FollowerProfileSnapshot> Profiles)> LoadNormalizedStateAsync(
        string sessionId,
        bool persistNormalized)
    {
        var roster = (await store.LoadRosterAsync(sessionId)).ToList();
        var profiles = (await store.LoadProfilesAsync(sessionId)).ToList();
        var (normalizedRoster, normalizedProfiles, changed) = NormalizeState(roster, profiles);
        if (persistNormalized && changed)
        {
            await store.SaveProfilesAsync(sessionId, normalizedProfiles);
            await store.SaveRosterAsync(sessionId, normalizedRoster);
            diagnosticsLog?.Append($"normalize-state session={sessionId} rosterBefore={roster.Count} rosterAfter={normalizedRoster.Count} profilesBefore={profiles.Count} profilesAfter={normalizedProfiles.Count}");
        }

        return (normalizedRoster, normalizedProfiles);
    }

    private async Task SaveNormalizedStateAsync(
        string sessionId,
        IReadOnlyList<FollowerRosterRecord> roster,
        IReadOnlyList<FollowerProfileSnapshot> profiles)
    {
        var (normalizedRoster, normalizedProfiles, changed) = NormalizeState(roster, profiles);
        await store.SaveProfilesAsync(sessionId, normalizedProfiles);
        await store.SaveRosterAsync(sessionId, normalizedRoster);
        if (changed)
        {
            diagnosticsLog?.Append($"normalize-save session={sessionId} rosterBefore={roster.Count} rosterAfter={normalizedRoster.Count} profilesBefore={profiles.Count} profilesAfter={normalizedProfiles.Count}");
        }
    }

    private static (List<FollowerRosterRecord> Roster, List<FollowerProfileSnapshot> Profiles, bool Changed) NormalizeState(
        IReadOnlyList<FollowerRosterRecord> roster,
        IReadOnlyList<FollowerProfileSnapshot> profiles)
    {
        var changed = false;
        var firstProfilesByAid = profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Aid))
            .GroupBy(profile => profile.Aid, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var nicknameGroups = roster
            .Where(member => !string.IsNullOrWhiteSpace(member.Aid) && !string.IsNullOrWhiteSpace(member.Nickname))
            .GroupBy(member => member.Nickname, StringComparer.OrdinalIgnoreCase)
            .Select(group => SelectCanonicalRosterMember(group.ToList(), firstProfilesByAid))
            .ToArray();

        var normalizedRoster = new List<FollowerRosterRecord>(nicknameGroups.Length);
        var seenAids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in nicknameGroups)
        {
            if (string.IsNullOrWhiteSpace(member.Aid)
                || string.IsNullOrWhiteSpace(member.Nickname)
                || !seenAids.Add(member.Aid))
            {
                changed = true;
                continue;
            }

            normalizedRoster.Add(member);
        }

        var validAids = normalizedRoster
            .Select(member => member.Aid)
            .ToHashSet(StringComparer.Ordinal);
        var normalizedProfiles = new List<FollowerProfileSnapshot>(profiles.Count);
        var seenProfileAids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Aid)
                || !validAids.Contains(profile.Aid)
                || !seenProfileAids.Add(profile.Aid))
            {
                changed = true;
                continue;
            }

            normalizedProfiles.Add(profile);
        }

        changed |= normalizedRoster.Count != roster.Count || normalizedProfiles.Count != profiles.Count;
        return (normalizedRoster, normalizedProfiles, changed);
    }

    private static FollowerRosterRecord SelectCanonicalRosterMember(
        IReadOnlyList<FollowerRosterRecord> duplicates,
        IReadOnlyDictionary<string, FollowerProfileSnapshot> profilesByAid)
    {
        return duplicates
            .Select((member, index) => new
            {
                Member = member,
                Index = index,
                Score = ScoreRosterMember(member, profilesByAid.GetValueOrDefault(member.Aid)),
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => candidate.Member)
            .First();
    }

    private static int ScoreRosterMember(FollowerRosterRecord member, FollowerProfileSnapshot? profile)
    {
        var score = 0;
        if (profile is not null)
        {
            score += 100;
            score += Math.Max(profile.Level, 0);
            score += Math.Min(Math.Max(profile.Experience, 0) / 1000, 20);
            if (profile.Equipment is not null)
            {
                score += 25;
                score += Math.Min(profile.Equipment.Items.Count, 25);
            }
        }

        if (member.AutoJoin)
        {
            score += 20;
        }

        if (string.Equals(member.LoadoutMode, FollowerLoadoutModes.Persisted, StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        if (!string.IsNullOrWhiteSpace(member.AssignedEquipmentBuildName))
        {
            score += 30;
        }

        return score;
    }

    private static string ResolveNickname(IReadOnlyCollection<FollowerRosterRecord> roster, string? requestedNickname)
    {
        if (!string.IsNullOrWhiteSpace(requestedNickname))
        {
            return requestedNickname.Trim();
        }

        for (var index = roster.Count + 1; index < roster.Count + 500; index++)
        {
            var candidate = $"Follower{index}";
            if (roster.All(existing => !string.Equals(existing.Nickname, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return $"Follower{Guid.NewGuid():N}"[..16];
    }

    private static void EnsureNicknameAvailable(IEnumerable<FollowerRosterRecord> roster, string nickname, string? aidToIgnore)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            throw new InvalidOperationException("Nickname cannot be empty.");
        }

        var existing = roster.FirstOrDefault(member =>
            !string.Equals(member.Aid, aidToIgnore, StringComparison.Ordinal)
            && string.Equals(member.Nickname, nickname.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            throw new InvalidOperationException($"Follower '{nickname.Trim()}' already exists.");
        }
    }

    private static string NormalizeLoadoutMode(string loadoutMode)
    {
        if (string.IsNullOrWhiteSpace(loadoutMode))
        {
            return FollowerLoadoutModes.Persisted;
        }

        return loadoutMode.Trim().ToLowerInvariant() switch
        {
            "persisted" => FollowerLoadoutModes.Persisted,
            "saved" => FollowerLoadoutModes.Persisted,
            "keep" => FollowerLoadoutModes.Persisted,
            "generated" => FollowerLoadoutModes.Generated,
            "default" => FollowerLoadoutModes.Generated,
            "random" => FollowerLoadoutModes.Generated,
            _ => throw new InvalidOperationException("Loadout mode must be 'persisted' or 'generated'."),
        };
    }

    private static FollowerRosterRecord FindRosterMember(IEnumerable<FollowerRosterRecord> roster, string aid)
    {
        return roster.FirstOrDefault(existing => string.Equals(existing.Aid, aid, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Follower '{aid}' was not found.");
    }

    private static void ReplaceRosterMember(List<FollowerRosterRecord> roster, FollowerRosterRecord member)
    {
        var index = roster.FindIndex(existing => string.Equals(existing.Aid, member.Aid, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException($"Follower '{member.Aid}' was not found.");
        }

        roster[index] = member;
    }

    private static void ReplaceProfile(
        List<FollowerProfileSnapshot> profiles,
        string aid,
        Func<FollowerProfileSnapshot, FollowerProfileSnapshot> update)
    {
        var index = profiles.FindIndex(existing => string.Equals(existing.Aid, aid, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        profiles[index] = update(profiles[index]);
    }

    private static void ReplaceOrAddProfile(List<FollowerProfileSnapshot> profiles, FollowerProfileSnapshot profile)
    {
        var index = profiles.FindIndex(existing => string.Equals(existing.Aid, profile.Aid, StringComparison.Ordinal));
        if (index < 0)
        {
            profiles.Add(profile);
            return;
        }

        profiles[index] = profile;
    }
}

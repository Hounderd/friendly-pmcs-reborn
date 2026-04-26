using System.Reflection;
using System.Text.Json;
using FriendlyPMC.Server.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerRosterStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string rootDirectory;

    public FollowerRosterStore(ModHelper modHelper)
        : this(Path.Combine(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "data"))
    {
    }

    public FollowerRosterStore(string rootDirectory)
    {
        this.rootDirectory = rootDirectory;
    }

    public void EnsureStorageRootExists()
    {
        Directory.CreateDirectory(rootDirectory);
    }

    public IReadOnlyList<string> GetKnownSessionIds()
    {
        if (!Directory.Exists(rootDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetDirectories(rootDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray()!;
    }

    public async Task<IReadOnlyList<FollowerRosterRecord>> LoadRosterAsync(string sessionId)
    {
        var path = GetRosterPath(sessionId);
        if (!File.Exists(path))
        {
            return Array.Empty<FollowerRosterRecord>();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var roster = await JsonSerializer.DeserializeAsync<List<FollowerRosterRecord>>(stream, SerializerOptions);
            return roster ?? new List<FollowerRosterRecord>();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            QuarantineUnreadableStoreFile(path);
            return Array.Empty<FollowerRosterRecord>();
        }
    }

    public async Task SaveRosterAsync(string sessionId, IReadOnlyList<FollowerRosterRecord> roster)
    {
        var path = GetRosterPath(sessionId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, roster, SerializerOptions);
    }

    public async Task<IReadOnlyList<FollowerProfileSnapshot>> LoadProfilesAsync(string sessionId)
    {
        var path = GetProfilesPath(sessionId);
        if (!File.Exists(path))
        {
            return Array.Empty<FollowerProfileSnapshot>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var profiles = JsonSerializer.Deserialize<List<FollowerProfileSnapshot>>(json, SerializerOptions)
                ?? new List<FollowerProfileSnapshot>();
            if (profiles.Any(profile => profile.Health?.Parts is null || profile.Health.Parts.Count == 0))
            {
                var legacyProfiles = JsonSerializer.Deserialize<List<LegacyFollowerProfileSnapshot>>(json, SerializerOptions)
                    ?? new List<LegacyFollowerProfileSnapshot>();
                if (legacyProfiles.Count == profiles.Count)
                {
                    profiles = profiles
                        .Zip(legacyProfiles, UpgradeLegacyProfileIfNeeded)
                        .ToList();
                }
            }

            profiles = profiles
                .Select(FollowerInventoryMigrationPolicy.Upgrade)
                .ToList();

            return profiles;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            QuarantineUnreadableStoreFile(path);
            return Array.Empty<FollowerProfileSnapshot>();
        }
    }

    private static void QuarantineUnreadableStoreFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var backupPath = $"{path}.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Move(path, backupPath, overwrite: false);
        }
        catch
        {
            // Keep startup moving even if the backup cannot be created.
        }
    }

    public async Task SaveProfilesAsync(string sessionId, IReadOnlyList<FollowerProfileSnapshot> profiles)
    {
        var path = GetProfilesPath(sessionId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profiles, SerializerOptions);
    }

    private string GetSessionDirectory(string sessionId) => Path.Combine(rootDirectory, sessionId);

    private string GetRosterPath(string sessionId) => Path.Combine(GetSessionDirectory(sessionId), "roster.json");

    private string GetProfilesPath(string sessionId) => Path.Combine(GetSessionDirectory(sessionId), "profiles.json");

    private static FollowerProfileSnapshot UpgradeLegacyProfileIfNeeded(
        FollowerProfileSnapshot current,
        LegacyFollowerProfileSnapshot legacy)
    {
        var needsHealthUpgrade = current.Health?.Parts is not { Count: > 0 };
        var needsLevelUpgrade = current.Level <= 0 && legacy.Level > 0;
        var needsEquipmentUpgrade = current.Equipment is null && legacy.Equipment is not null;
        if (!needsHealthUpgrade && !needsLevelUpgrade && !needsEquipmentUpgrade)
        {
            return current;
        }

        var upgradedParts = current.Health?.Parts is { Count: > 0 }
            ? new Dictionary<string, HealthPartSnapshot>(current.Health.Parts, StringComparer.Ordinal)
            : new Dictionary<string, HealthPartSnapshot>(StringComparer.Ordinal);
        if (needsHealthUpgrade && legacy.Health.Head is not null)
        {
            upgradedParts["Head"] = legacy.Health.Head;
        }

        return current with
        {
            Level = needsLevelUpgrade ? legacy.Level : current.Level,
            Health = new FollowerHealthSnapshot(upgradedParts),
            Equipment = needsEquipmentUpgrade ? UpgradeLegacyEquipment(legacy) : current.Equipment,
        };
    }

    private static FollowerEquipmentSnapshot? UpgradeLegacyEquipment(LegacyFollowerProfileSnapshot legacy)
    {
        if (legacy.Equipment is null || string.IsNullOrWhiteSpace(legacy.Equipment.EquipmentId))
        {
            return null;
        }

        return new FollowerEquipmentSnapshot(
            legacy.Equipment.EquipmentId,
            legacy.Equipment.Items
                .Select(item => new FollowerEquipmentItemSnapshot(
                    item.Id,
                    item.TemplateId,
                    item.ParentId,
                    item.SlotId,
                    item.LocationJson,
                    item.UpdJson))
                .ToArray());
    }

    private sealed record LegacyFollowerHealthSnapshot(HealthPartSnapshot? Head);

    private sealed record LegacyFollowerEquipmentItemSnapshot(
        string Id,
        string TemplateId,
        string? ParentId,
        string? SlotId,
        string? LocationJson,
        string? UpdJson);

    private sealed record LegacyFollowerEquipmentSnapshot(
        string EquipmentId,
        IReadOnlyList<LegacyFollowerEquipmentItemSnapshot> Items);

    private sealed record LegacyFollowerProfileSnapshot(
        string Aid,
        string Nickname,
        string Side,
        int Level,
        int Experience,
        IReadOnlyDictionary<string, int> SkillProgress,
        IReadOnlyList<string> InventoryItemIds,
        LegacyFollowerHealthSnapshot Health,
        LegacyFollowerEquipmentSnapshot? Equipment);
}

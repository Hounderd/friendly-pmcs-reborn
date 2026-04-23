using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class PlayerProfileIntegrityService(
    ProfileHelper? profileHelper = null,
    DatabaseService? databaseService = null,
    SaveServer? saveServer = null,
    FollowerDiagnosticsLog? diagnosticsLog = null)
{
    public async Task<int> RepairAllLoadedProfilesAsync()
    {
        if (profileHelper is null)
        {
            return 0;
        }

        var repairedProfiles = 0;
        foreach (var sessionKey in profileHelper.GetProfiles().Keys)
        {
            var sessionId = sessionKey is MongoId mongoId
                ? mongoId
                : new MongoId(sessionKey.ToString());
            if (await RepairProfileAsync(sessionId))
            {
                repairedProfiles++;
            }
        }

        return repairedProfiles;
    }

    public async Task<bool> RepairProfileAsync(MongoId sessionId)
    {
        if (profileHelper is null)
        {
            return false;
        }

        var pmcProfile = profileHelper.GetPmcProfile(sessionId);
        if (pmcProfile is null)
        {
            return false;
        }

        var templates = databaseService?.GetItems()
            .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value, StringComparer.Ordinal);
        var changed = FollowerInventoryIdIntegrityPolicy.NormalizePlayerProfileInventory(pmcProfile, templates);
        if (!changed)
        {
            return false;
        }

        if (saveServer is not null)
        {
            await saveServer.SaveProfileAsync(sessionId);
        }

        diagnosticsLog?.Append($"player-inventory-heal session={sessionId} items={pmcProfile.Inventory?.Items?.Count ?? 0}");
        return true;
    }
}

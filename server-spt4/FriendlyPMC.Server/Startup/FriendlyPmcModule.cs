using FriendlyPMC.Server.Services;
using FriendlyPMC.Server.Patches;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Startup;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public sealed class FriendlyPmcModule(
    ISptLogger<FriendlyPmcModule> logger,
    FollowerRosterStore rosterStore,
    FollowerManagerSocialViewService socialViewService,
    PlayerProfileIntegrityService playerProfileIntegrityService)
    : IOnLoad
{
    public async Task OnLoad()
    {
        rosterStore.EnsureStorageRootExists();
        var repairedProfiles = await playerProfileIntegrityService.RepairAllLoadedProfilesAsync();
        FollowerServerHarmonyBridge.Initialize(socialViewService, playerProfileIntegrityService, message => logger.Error(message));
        FollowerServerSocialPatches.Apply();
        if (repairedProfiles > 0)
        {
            logger.Success($"Repaired player inventory ids for {repairedProfiles} loaded profile(s)");
        }

        logger.Success("PMC Squadmates services loaded");
    }
}

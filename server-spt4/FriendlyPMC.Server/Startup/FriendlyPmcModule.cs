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
    FollowerManagerSocialViewService socialViewService)
    : IOnLoad
{
    public Task OnLoad()
    {
        rosterStore.EnsureStorageRootExists();
        FollowerServerHarmonyBridge.Initialize(socialViewService, message => logger.Error(message));
        FollowerServerSocialPatches.Apply();
        logger.Success("FriendlyPMC core follower services loaded");
        return Task.CompletedTask;
    }
}

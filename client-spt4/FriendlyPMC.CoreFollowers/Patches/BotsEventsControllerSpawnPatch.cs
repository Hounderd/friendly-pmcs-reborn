#if SPT_CLIENT
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class BotsEventsControllerSpawnPatch : ModulePatch
{
    public bool Enabled { get; private set; }

    public new void Enable()
    {
        base.Enable();
        Enabled = true;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotsEventsController), "SpawnAction");
    }

    [PatchPostfix]
    private static void PatchPostfix()
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return;
        }

        try
        {
            plugin.QueueRaidFollowerSpawn();
        }
        catch (System.Exception ex)
        {
            plugin.LogPluginError("Failed to queue persisted follower raid spawn", ex);
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class BotsEventsControllerSpawnPatch
{
    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
    }
}
#endif

#if SPT_CLIENT
using System.Reflection;
using EFT;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class RaidEndPatch : ModulePatch
{
    public bool Enabled { get; private set; }

    public new void Enable()
    {
        base.Enable();
        Enabled = true;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LocalGame), "CleanUp");
    }

    [PatchPrefix]
    private static void PatchPrefix()
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return;
        }

        try
        {
            plugin.RaidController.SaveRaidProgressAsync().GetAwaiter().GetResult();
            plugin.LogPluginInfo("Saved follower raid progress");
        }
        catch (System.Exception ex)
        {
            plugin.LogPluginError("Failed to save follower raid progress", ex);
        }
        finally
        {
            FollowerPlayerShotMemory.Reset();
            BotsControllerStatePatch.Clear();
            plugin.RaidController.Reset();
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class RaidEndPatch
{
    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
    }
}
#endif

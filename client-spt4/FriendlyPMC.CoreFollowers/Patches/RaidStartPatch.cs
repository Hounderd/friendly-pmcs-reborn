#if SPT_CLIENT
using System.Reflection;
using EFT;
using EFT.UI.Matchmaker;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class RaidStartPatch : ModulePatch
{
    public bool Enabled { get; private set; }

    public new void Enable()
    {
        base.Enable();
        Enabled = true;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(MatchMakerAcceptScreen), "Show", new[] { typeof(ISession), typeof(RaidSettings), typeof(RaidSettings) });
    }

    [PatchPostfix]
    private static void PatchPostfix(RaidSettings raidSettings)
    {
        if (!raidSettings.IsPmc || FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return;
        }

        try
        {
            FollowerPlayerShotMemory.Reset();
            plugin.RaidController.Reset();
            plugin.RaidController.PrimeRaidFollowersAsync().GetAwaiter().GetResult();
            plugin.LogPluginInfo($"Loaded {plugin.RaidController.PendingFollowers.Count} pending follower snapshots");
        }
        catch (System.Exception ex)
        {
            plugin.LogPluginError("Failed to prepare raid followers", ex);
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class RaidStartPatch
{
    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
    }
}
#endif

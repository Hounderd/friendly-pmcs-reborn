#if SPT_CLIENT
using System.Reflection;
using EFT;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class DebugFollowerBossSetupPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotBoss), nameof(BotBoss.SetBoss));
    }

    [PatchPrefix]
    private static bool PatchPrefix(BotBoss __instance)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return true;
        }

        var ownerProfileId = __instance?.Owner?.ProfileId;
        var shouldSkip = DebugFollowerBossSetupPolicy.ShouldSkipNativeBossSetup(
            ownerProfileId,
            plugin.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId));

        if (!shouldSkip)
        {
            return true;
        }

        plugin.LogPluginInfo($"Skipped native BotBoss.SetBoss for PMC Squadmates follower {ownerProfileId}");
        return false;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class DebugFollowerBossSetupPatch
{
    public void Enable()
    {
    }
}
#endif

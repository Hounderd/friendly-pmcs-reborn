#if SPT_CLIENT
using System.Reflection;
using EFT;
using FriendlyPMC.CoreFollowers.Modules;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class BotsControllerPatch : ModulePatch
{
    public bool Enabled { get; private set; }

    public new void Enable()
    {
        base.Enable();
        Enabled = true;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotOwner), "method_10");
    }

    [PatchPostfix]
    private static void PatchPostfix(BotOwner __instance)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin || GamePlayerOwner.MyPlayer is not { } localPlayer)
        {
            return;
        }

        if (__instance is null || __instance.IsDead || __instance.GetPlayer is null || __instance.ProfileId == localPlayer.ProfileId)
        {
            return;
        }

        if (__instance.Side != localPlayer.Side || plugin.RaidController.PendingFollowers.Count == 0)
        {
            return;
        }

        try
        {
            var attached = plugin.RaidController.TryAttachPendingFollower(
                snapshot => new BotOwnerFollowerRuntimeHandle(__instance, snapshot));

            if (!attached)
            {
                return;
            }

            __instance.Memory.DeleteInfoAboutEnemy(localPlayer);
            plugin.LogPluginInfo($"Attached pending follower to spawned bot {__instance.Profile.Nickname}");
        }
        catch (System.Exception ex)
        {
            plugin.LogPluginError("Failed to attach pending follower to spawned bot", ex);
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class BotsControllerPatch
{
    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
    }
}
#endif

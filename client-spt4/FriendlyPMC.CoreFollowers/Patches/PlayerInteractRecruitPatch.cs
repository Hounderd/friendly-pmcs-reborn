#if SPT_CLIENT
using System.Reflection;
using EFT;
using FriendlyPMC.CoreFollowers.Modules;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class PlayerInteractRecruitPatch : ModulePatch
{
    public bool Enabled { get; private set; }

    public new void Enable()
    {
        base.Enable();
        Enabled = true;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotGroupRequestController), "TryAskFollowMeRequest");
    }

    [PatchPostfix]
    private static void PatchPostfix(bool __result, IPlayer player, BotOwner posibleExecuter)
    {
        if (!__result || FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin || GamePlayerOwner.MyPlayer is not { } localPlayer)
        {
            return;
        }

        if (player?.ProfileId != localPlayer.ProfileId || posibleExecuter is null || posibleExecuter.IsDead)
        {
            return;
        }

        if (plugin.Registry.IsManagedRuntimeProfileId(posibleExecuter.ProfileId))
        {
            return;
        }

        var runtimeFollower = new BotOwnerFollowerRuntimeHandle(posibleExecuter);
        if (plugin.Registry.Contains(runtimeFollower.Aid))
        {
            return;
        }

        try
        {
            plugin.RegisterRecruit(runtimeFollower);
            FollowerLootingRuntimeDisabler.DisableForFollower(posibleExecuter, plugin.LogPluginInfo);
            plugin.LogPluginInfo($"Registered recruited follower {runtimeFollower.CaptureSnapshot().Nickname}");
        }
        catch (System.Exception ex)
        {
            plugin.LogPluginError("Failed to register recruited follower", ex);
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class PlayerInteractRecruitPatch
{
    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
    }
}
#endif

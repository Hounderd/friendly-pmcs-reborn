#if SPT_CLIENT
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class BotOwnerUpdatePatch : ModulePatch
{
    public bool Enabled { get; private set; }

    public new void Enable()
    {
        base.Enable();
        Enabled = true;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotOwner), "UpdateManual");
    }

    [PatchPostfix]
    private static void PatchPostfix(BotOwner __instance)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin || __instance is null || string.IsNullOrEmpty(__instance.ProfileId))
        {
            return;
        }

        if (!plugin.Registry.TryGetRuntimeByProfileId(__instance.ProfileId, out var runtimeFollower))
        {
            return;
        }

        if (!runtimeFollower.IsOperational)
        {
            plugin.Registry.RemoveRuntime(runtimeFollower.Aid);
            return;
        }

        runtimeFollower.TickOrder();
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class BotOwnerUpdatePatch
{
    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
    }
}
#endif

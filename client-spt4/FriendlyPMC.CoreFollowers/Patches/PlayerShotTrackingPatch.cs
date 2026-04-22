#if SPT_CLIENT
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using FriendlyPMC.CoreFollowers.Services;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class PlayerShotTrackingPatch : ModulePatch
{
    public bool Enabled { get; private set; }

    public new void Enable()
    {
        base.Enable();
        Enabled = true;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), "OnMakingShot");
    }

    [PatchPostfix]
    private static void PatchPostfix(Player __instance, UnityEngine.Vector3 force)
    {
        if (!ReferenceEquals(__instance, GamePlayerOwner.MyPlayer))
        {
            return;
        }

        FollowerPlayerShotMemory.RecordShot(__instance, force);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class PlayerShotTrackingPatch
{
    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
    }
}
#endif

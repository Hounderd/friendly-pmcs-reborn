#if SPT_CLIENT
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class BotsControllerStatePatch : ModulePatch
{
    public static BotsController? ActiveController { get; private set; }

    public bool Enabled { get; private set; }

    public new void Enable()
    {
        base.Enable();
        Enabled = true;
    }

    public static void Clear()
    {
        ActiveController = null;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotsController), "AddActivePLayer");
    }

    [PatchPostfix]
    private static void PatchPostfix(BotsController __instance)
    {
        ActiveController = __instance;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class BotsControllerStatePatch
{
    public bool Enabled { get; private set; }

    public static object? ActiveController => null;

    public void Enable()
    {
        Enabled = true;
    }

    public static void Clear()
    {
    }
}
#endif

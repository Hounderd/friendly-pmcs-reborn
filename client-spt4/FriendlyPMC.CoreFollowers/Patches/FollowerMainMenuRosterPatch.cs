#if SPT_CLIENT
using System.Reflection;
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FollowerMainMenuRosterPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var screenType = AccessTools.TypeByName("EFT.UI.MenuScreen")!;
        return AccessTools.GetDeclaredMethods(screenType)
            .First(method =>
                method.Name == "Show"
                && method.GetParameters().Length == 3
                && method.GetParameters()[0].ParameterType == typeof(Profile));
    }

    [PatchPostfix]
    private static void PatchPostfix(object __instance, Profile profile)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return;
        }

        FollowerMainMenuRosterInjector.TryInject(
            __instance,
            profile,
            ResolveFollowers(plugin),
            plugin.LogPluginInfo,
            plugin.LogPluginError);
    }

    private static IReadOnlyList<FollowerSnapshotDto> ResolveFollowers(FriendlyPmcCoreFollowersPlugin plugin)
    {
        return plugin.Registry.Followers
            .Where(follower => !string.IsNullOrWhiteSpace(follower.Nickname))
            .ToArray();
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FollowerMainMenuRosterPatch
{
    public void Enable()
    {
    }
}
#endif

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

        var followers = ResolveFollowers(plugin);
        plugin.LogPluginInfo($"Follower main menu roster resolved: count={followers.Count}");

        FollowerMainMenuRosterInjector.TryInject(
            __instance,
            profile,
            followers,
            plugin.LogPluginInfo,
            plugin.LogPluginError);
    }

    private static IReadOnlyList<FollowerSnapshotDto> ResolveFollowers(FriendlyPmcCoreFollowersPlugin plugin)
    {
        try
        {
            var persistedRoster = plugin.ApiClient.GetFollowerManagerRosterAsync().GetAwaiter().GetResult();
            if (persistedRoster.Count > 0)
            {
                return persistedRoster;
            }
        }
        catch (Exception ex)
        {
            plugin.LogPluginError("Failed to load follower manager roster for main menu", ex);
        }

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

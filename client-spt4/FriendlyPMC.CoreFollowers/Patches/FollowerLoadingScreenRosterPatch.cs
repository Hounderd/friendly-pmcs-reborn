#if SPT_CLIENT
using System.Reflection;
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FollowerLoadingScreenRosterPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var screenType = AccessTools.TypeByName("EFT.UI.Matchmaker.MatchmakerTimeHasCome")!;
        return AccessTools.GetDeclaredMethods(screenType)
            .First(method =>
                method.Name == "Show"
                && method.GetParameters().Length == 3
                && method.GetParameters()[1].ParameterType == typeof(RaidSettings));
    }

    [PatchPostfix]
    private static void PatchPostfix(object __instance)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return;
        }

        FollowerLoadingScreenRosterInjector.TryInject(
            __instance,
            ResolveFollowers(plugin),
            plugin.LogPluginInfo,
            plugin.LogPluginError);
    }

    private static IReadOnlyList<FollowerSnapshotDto> ResolveFollowers(FriendlyPmcCoreFollowersPlugin plugin)
    {
        var followers = new List<FollowerSnapshotDto>();
        var seenAids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var follower in plugin.RaidController.PendingFollowers)
        {
            if (seenAids.Add(follower.Aid))
            {
                followers.Add(follower);
            }
        }

        foreach (var follower in plugin.Registry.Followers)
        {
            if (seenAids.Add(follower.Aid))
            {
                followers.Add(follower);
            }
        }

        return followers;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FollowerLoadingScreenRosterPatch
{
    public void Enable()
    {
    }
}
#endif

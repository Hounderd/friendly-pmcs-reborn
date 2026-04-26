#if SPT_CLIENT
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FriendlyPlayerHostilityPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotsGroup), nameof(BotsGroup.IsPlayerEnemy));
    }

    [PatchPrefix]
    private static bool PatchPrefix(BotsGroup __instance, IPlayer player, ref bool __result)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return true;
        }

        var localPlayer = GamePlayerOwner.MyPlayer;
        if (localPlayer is null || player is null)
        {
            return true;
        }

        var groupMemberProfileIds = __instance.Members
            .Where(member => member?.ProfileId is not null)
            .Select(member => member!.ProfileId)
            .ToArray();
        var registeredFollowerProfileIds = plugin.Registry.RuntimeFollowers
            .Select(follower => follower.RuntimeProfileId)
            .ToArray();

        var decision = Services.FollowerAlliancePolicy.GetPlayerEnemyOverride(
            __instance.InitialBot?.ProfileId,
            groupMemberProfileIds,
            registeredFollowerProfileIds,
            localPlayer.ProfileId,
            player.ProfileId);

        if (!decision.HasValue)
        {
            return true;
        }

        plugin.LogPluginInfo(
            $"FriendlyPlayerHostilityPatch override: initialBot={__instance.InitialBot?.ProfileId}, target={player.ProfileId}, decision={decision.Value}");
        __result = decision.Value;
        return false;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FriendlyPlayerHostilityPatch
{
    public void Enable()
    {
    }
}
#endif

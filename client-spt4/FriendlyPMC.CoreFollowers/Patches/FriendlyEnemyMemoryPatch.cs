#if SPT_CLIENT
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FriendlyEnemyMemoryPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotMemoryClass), nameof(BotMemoryClass.AddEnemy));
    }

    [PatchPrefix]
    private static bool PatchPrefix(BotMemoryClass __instance, IPlayer enemy)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return true;
        }

        var localPlayer = GamePlayerOwner.MyPlayer;
        var botProfileId = __instance.BotOwner_0?.ProfileId;
        var targetProfileId = enemy?.ProfileId;
        var shouldProtect = Services.FollowerProtectionPolicy.ShouldProtectPlayer(
            botProfileId,
            plugin.Registry.Followers.Select(follower => follower.Aid),
            localPlayer?.ProfileId,
            targetProfileId);

        if (!shouldProtect)
        {
            return true;
        }

        plugin.LogPluginInfo(
            $"FriendlyEnemyMemoryPatch blocked AddEnemy: bot={botProfileId}, target={targetProfileId}");
        return false;
    }
}

public sealed class FriendlyEnemyControllerPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotEnemiesController), nameof(BotEnemiesController.IsEnemy));
    }

    [PatchPrefix]
    private static bool PatchPrefix(BotEnemiesController __instance, IPlayer player, ref bool __result)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return true;
        }

        var localPlayer = GamePlayerOwner.MyPlayer;
        var botProfileId = __instance.BotOwner_0?.ProfileId;
        var targetProfileId = player?.ProfileId;
        var shouldProtect = Services.FollowerProtectionPolicy.ShouldProtectPlayer(
            botProfileId,
            plugin.Registry.Followers.Select(follower => follower.Aid),
            localPlayer?.ProfileId,
            targetProfileId);

        if (!shouldProtect)
        {
            return true;
        }

        plugin.LogPluginInfo(
            $"FriendlyEnemyControllerPatch override: bot={botProfileId}, target={targetProfileId}");
        __result = false;
        return false;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FriendlyEnemyMemoryPatch
{
    public void Enable()
    {
    }
}

public sealed class FriendlyEnemyControllerPatch
{
    public void Enable()
    {
    }
}
#endif

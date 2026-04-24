#if SPT_CLIENT
using System.Reflection;
using EFT;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class SainEnemyCheckAddPatch : ModulePatch
{
    private static readonly Type? SainEnemyControllerType = AccessTools.TypeByName("SAIN.SAINComponent.Classes.EnemyClasses.SAINEnemyController");
    private static readonly PropertyInfo? BotOwnerProperty = SainEnemyControllerType is null ? null : AccessTools.Property(SainEnemyControllerType, "BotOwner");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(SainEnemyControllerType!, "CheckAddEnemy");
    }

    [PatchPrefix]
    private static bool PatchPrefix(object __instance, IPlayer IPlayer, ref object? __result)
    {
        if (!ShouldSuppress(__instance, IPlayer?.ProfileId))
        {
            return true;
        }

        if (FriendlyPmcCoreFollowersPlugin.Instance is { EnableCombatTraceDiagnostics: true } plugin)
        {
            plugin.LogPluginInfo(
                $"SAIN CheckAddEnemy suppressed protected target: bot={GetBotProfileId(__instance)}, target={IPlayer?.ProfileId}");
        }

        __result = null;
        return false;
    }

    private static bool ShouldSuppress(object instance, string? targetProfileId)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin || string.IsNullOrWhiteSpace(targetProfileId))
        {
            return false;
        }

        var botProfileId = GetBotProfileId(instance);
        return SainFollowerProtectionPolicy.ShouldSuppressProtectedEnemy(
            botProfileId,
            plugin.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId),
            GamePlayerOwner.MyPlayer?.ProfileId,
            targetProfileId);
    }

    private static string? GetBotProfileId(object instance)
    {
        var botOwner = BotOwnerProperty?.GetValue(instance);
        return botOwner?.GetType().GetProperty("ProfileId")?.GetValue(botOwner) as string;
    }
}

public sealed class SainEnemyManualUpdatePatch : ModulePatch
{
    private static readonly Type? SainEnemyControllerType = AccessTools.TypeByName("SAIN.SAINComponent.Classes.EnemyClasses.SAINEnemyController");
    private static readonly PropertyInfo? BotOwnerProperty = SainEnemyControllerType is null ? null : AccessTools.Property(SainEnemyControllerType, "BotOwner");
    private static readonly PropertyInfo? GoalEnemyProperty = SainEnemyControllerType is null ? null : AccessTools.Property(SainEnemyControllerType, "GoalEnemy");
    private static readonly MethodInfo? RemoveEnemyMethod = SainEnemyControllerType is null ? null : AccessTools.Method(SainEnemyControllerType, "RemoveEnemy");
    private static readonly MethodInfo? ClearEnemyMethod = SainEnemyControllerType is null ? null : AccessTools.Method(SainEnemyControllerType, "ClearEnemy");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(SainEnemyControllerType!, "ManualUpdate");
    }

    [PatchPostfix]
    private static void PatchPostfix(object __instance)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return;
        }

        var botOwner = BotOwnerProperty?.GetValue(__instance);
        var botProfileId = botOwner?.GetType().GetProperty("ProfileId")?.GetValue(botOwner) as string;
        if (string.IsNullOrWhiteSpace(botProfileId))
        {
            return;
        }

        if (!plugin.Registry.TryGetRuntimeByProfileId(botProfileId, out _))
        {
            return;
        }

        var goalEnemy = GoalEnemyProperty?.GetValue(__instance);
        var targetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(goalEnemy);
        var shouldSuppress = SainFollowerProtectionPolicy.ShouldSuppressProtectedEnemy(
            botProfileId,
            plugin.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId),
            GamePlayerOwner.MyPlayer?.ProfileId,
            targetProfileId);

        if (!shouldSuppress || string.IsNullOrWhiteSpace(targetProfileId))
        {
            return;
        }

        RemoveEnemyMethod?.Invoke(__instance, new object[] { targetProfileId });
        ClearEnemyMethod?.Invoke(__instance, Array.Empty<object>());

        if (botOwner?.GetType().GetProperty("Memory")?.GetValue(botOwner) is { } memory)
        {
            var goalEnemyProperty = memory.GetType().GetProperty("GoalEnemy");
            goalEnemyProperty?.SetValue(memory, null);
        }

        if (plugin.EnableCombatTraceDiagnostics)
        {
            plugin.LogPluginInfo(
                $"SAIN GoalEnemy cleared for protected target: bot={botProfileId}, target={targetProfileId}");
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class SainEnemyCheckAddPatch
{
    public void Enable()
    {
    }
}

public sealed class SainEnemyManualUpdatePatch
{
    public void Enable()
    {
    }
}
#endif

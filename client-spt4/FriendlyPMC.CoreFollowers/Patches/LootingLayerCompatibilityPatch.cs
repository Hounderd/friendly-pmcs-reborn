#if SPT_CLIENT
using System.Reflection;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class LootingLayerCompatibilityPatch : ModulePatch
{
    public static bool IsAvailable => AccessTools.TypeByName("LootingBots.LootingLayer") is not null;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("LootingBots.LootingLayer")!, "IsActive");
    }

    [PatchPrefix]
    private static bool PatchPrefix(object __instance, ref bool __result)
    {
        if (ShouldSuppressLooting(__instance))
        {
            __result = false;
            return false;
        }

        return true;
    }

    private static bool ShouldSuppressLooting(object instance)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return false;
        }

        var botOwner = AccessTools.Property(instance.GetType().BaseType, "BotOwner")?.GetValue(instance);
        var profileId = botOwner?.GetType().GetProperty("ProfileId")?.GetValue(botOwner) as string;
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return false;
        }

        if (!plugin.Registry.TryGetActiveOrderByProfileId(profileId, out var activeOrder))
        {
            return false;
        }

        return FollowerLootingCompatibilityPolicy.ShouldSuppressLooting(activeOrder);
    }
}

public sealed class LootingLayerEndingCompatibilityPatch : ModulePatch
{
    public static bool IsAvailable => AccessTools.TypeByName("LootingBots.LootingLayer") is not null;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("LootingBots.LootingLayer")!, "IsCurrentActionEnding");
    }

    [PatchPostfix]
    private static void PatchPostfix(object __instance, ref bool __result)
    {
        if (LootingLayerCompatibilityPatch.IsAvailable && ShouldForceEnd(__instance))
        {
            __result = true;
        }
    }

    private static bool ShouldForceEnd(object instance)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin)
        {
            return false;
        }

        var botOwner = AccessTools.Property(instance.GetType().BaseType, "BotOwner")?.GetValue(instance);
        var profileId = botOwner?.GetType().GetProperty("ProfileId")?.GetValue(botOwner) as string;
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return false;
        }

        if (!plugin.Registry.TryGetActiveOrderByProfileId(profileId, out var activeOrder))
        {
            return false;
        }

        return FollowerLootingCompatibilityPolicy.ShouldSuppressLooting(activeOrder);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class LootingLayerCompatibilityPatch
{
    public static bool IsAvailable => false;

    public void Enable()
    {
    }
}

public sealed class LootingLayerEndingCompatibilityPatch
{
    public static bool IsAvailable => false;

    public void Enable()
    {
    }
}
#endif

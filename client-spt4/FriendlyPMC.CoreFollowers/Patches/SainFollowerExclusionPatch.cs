#if SPT_CLIENT
using System.Reflection;
using EFT;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class SainIsBotExcludedByProfileIdPatch : ModulePatch
{
    private static readonly System.Type? SainEnableClassType = AccessTools.TypeByName("SAIN.SAINEnableClass");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(SainEnableClassType!, "IsBotExcluded", new[] { typeof(string) });
    }

    [PatchPrefix]
    private static bool PatchPrefix(string profileId, ref bool __result)
    {
        if (!ShouldExclude(profileId))
        {
            return true;
        }

        __result = true;
        return false;
    }

    private static bool ShouldExclude(string? profileId)
    {
        return SainFollowerExclusionPolicy.ShouldExcludeFollower(
            FriendlyPmcCoreFollowersPlugin.Instance?.Registry,
            profileId);
    }
}

public sealed class SainIsBotExcludedByOwnerPatch : ModulePatch
{
    private static readonly System.Type? SainEnableClassType = AccessTools.TypeByName("SAIN.SAINEnableClass");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(SainEnableClassType!, "IsBotExcluded", new[] { typeof(BotOwner) });
    }

    [PatchPrefix]
    private static bool PatchPrefix(BotOwner botOwner, ref bool __result)
    {
        if (!ShouldExclude(botOwner?.ProfileId))
        {
            return true;
        }

        __result = true;
        return false;
    }

    private static bool ShouldExclude(string? profileId)
    {
        return SainFollowerExclusionPolicy.ShouldExcludeFollower(
            FriendlyPmcCoreFollowersPlugin.Instance?.Registry,
            profileId);
    }
}

public sealed class SainIsDisabledForBotOwnerPatch : ModulePatch
{
    private static readonly System.Type? SainEnableClassType = AccessTools.TypeByName("SAIN.SAINEnableClass");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(SainEnableClassType!, "IsSAINDisabledForBot", new[] { typeof(BotOwner) });
    }

    [PatchPrefix]
    private static bool PatchPrefix(BotOwner botOwner, ref bool __result)
    {
        if (!ShouldExclude(botOwner?.ProfileId))
        {
            return true;
        }

        __result = true;
        return false;
    }

    private static bool ShouldExclude(string? profileId)
    {
        return SainFollowerExclusionPolicy.ShouldExcludeFollower(
            FriendlyPmcCoreFollowersPlugin.Instance?.Registry,
            profileId);
    }
}

public sealed class SainIsDisabledForPlayerPatch : ModulePatch
{
    private static readonly System.Type? SainEnableClassType = AccessTools.TypeByName("SAIN.SAINEnableClass");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(SainEnableClassType!, "IsSAINDisabledForBot", new[] { typeof(IPlayer) });
    }

    [PatchPrefix]
    private static bool PatchPrefix(IPlayer iPlayer, ref bool __result)
    {
        if (!ShouldExclude(iPlayer?.ProfileId))
        {
            return true;
        }

        __result = true;
        return false;
    }

    private static bool ShouldExclude(string? profileId)
    {
        return SainFollowerExclusionPolicy.ShouldExcludeFollower(
            FriendlyPmcCoreFollowersPlugin.Instance?.Registry,
            profileId);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class SainIsBotExcludedByProfileIdPatch
{
    public void Enable()
    {
    }
}

public sealed class SainIsBotExcludedByOwnerPatch
{
    public void Enable()
    {
    }
}

public sealed class SainIsDisabledForBotOwnerPatch
{
    public void Enable()
    {
    }
}

public sealed class SainIsDisabledForPlayerPatch
{
    public void Enable()
    {
    }
}
#endif

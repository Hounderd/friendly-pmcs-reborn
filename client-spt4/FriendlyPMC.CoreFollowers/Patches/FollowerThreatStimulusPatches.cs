#if SPT_CLIENT
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using Systems.Effects;
using FriendlyPMC.CoreFollowers.Services;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class HostileShotStimulusPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), "OnMakingShot");
    }

    [PatchPostfix]
    private static void PatchPostfix(Player __instance)
    {
        if (__instance is null || ReferenceEquals(__instance, GamePlayerOwner.MyPlayer))
        {
            return;
        }

        var origin = __instance.Fireport is not null
            ? __instance.Fireport.position
            : __instance.Transform.position;
        FollowerThreatStimulusRuntimeService.NotifyHostileShot(__instance, origin);
    }
}

public sealed class BulletNearMissStimulusPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(EffectsCommutator), "PlayHitEffect");
    }

    [PatchPostfix]
    private static void PatchPostfix(EffectsCommutator __instance, EftBulletClass info, ShotInfoClass playerHitInfo)
    {
        var attacker = info?.Player?.iPlayer;
        if (__instance is null || info is null || attacker is null)
        {
            return;
        }

        if (__instance.IsHitPointAlreadyProcessed(info.HitPoint))
        {
            return;
        }

        FollowerThreatStimulusRuntimeService.NotifyBulletNearMiss(attacker, info.HitPoint);
    }
}

public sealed class HostileFootstepStimulusPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), "PlayStepSound");
    }

    [PatchPostfix]
    private static void PatchPostfix(Player __instance)
    {
        if (__instance is null || ReferenceEquals(__instance, GamePlayerOwner.MyPlayer))
        {
            return;
        }

        FollowerThreatStimulusRuntimeService.NotifyHostileFootstep(__instance, __instance.Transform.position);
    }
}

public sealed class HostileVoiceLineStimulusPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), "Say");
    }

    [PatchPostfix]
    private static void PatchPostfix(Player __instance)
    {
        if (__instance is null || ReferenceEquals(__instance, GamePlayerOwner.MyPlayer))
        {
            return;
        }

        FollowerThreatStimulusRuntimeService.NotifyHostileVoiceLine(__instance, __instance.Transform.position);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class HostileShotStimulusPatch
{
    public void Enable()
    {
    }
}

public sealed class BulletNearMissStimulusPatch
{
    public void Enable()
    {
    }
}

public sealed class HostileFootstepStimulusPatch
{
    public void Enable()
    {
    }
}

public sealed class HostileVoiceLineStimulusPatch
{
    public void Enable()
    {
    }
}
#endif

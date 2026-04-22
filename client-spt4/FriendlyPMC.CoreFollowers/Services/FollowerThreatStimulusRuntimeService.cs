#if SPT_CLIENT
using EFT;
using FriendlyPMC.CoreFollowers.Modules;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerThreatStimulusRuntimeService
{
    public static void NotifyHostileShot(Player attacker, Vector3 stimulusPosition)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin || attacker is null)
        {
            return;
        }

        foreach (var follower in plugin.Registry.RuntimeFollowers)
        {
            if (follower is BotOwnerFollowerRuntimeHandle runtimeHandle)
            {
                runtimeHandle.HandleThreatStimulus(
                    attacker,
                    FollowerThreatStimulusType.HeardHostileShot,
                    stimulusPosition);
            }
        }
    }

    public static void NotifyBulletNearMiss(IPlayer attacker, Vector3 stimulusPosition)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin || attacker is null)
        {
            return;
        }

        foreach (var follower in plugin.Registry.RuntimeFollowers)
        {
            if (follower is BotOwnerFollowerRuntimeHandle runtimeHandle)
            {
                runtimeHandle.HandleThreatStimulus(
                    attacker,
                    FollowerThreatStimulusType.BulletNearMiss,
                    stimulusPosition);
            }
        }
    }

    public static void NotifyHostileFootstep(Player attacker, Vector3 stimulusPosition)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin || attacker is null)
        {
            return;
        }

        foreach (var follower in plugin.Registry.RuntimeFollowers)
        {
            if (follower is BotOwnerFollowerRuntimeHandle runtimeHandle)
            {
                runtimeHandle.HandleThreatStimulus(
                    attacker,
                    FollowerThreatStimulusType.HostileFootstep,
                    stimulusPosition);
            }
        }
    }

    public static void NotifyHostileVoiceLine(Player attacker, Vector3 stimulusPosition)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin || attacker is null)
        {
            return;
        }

        foreach (var follower in plugin.Registry.RuntimeFollowers)
        {
            if (follower is BotOwnerFollowerRuntimeHandle runtimeHandle)
            {
                runtimeHandle.HandleThreatStimulus(
                    attacker,
                    FollowerThreatStimulusType.HostileVoiceLine,
                    stimulusPosition);
            }
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerThreatStimulusRuntimeService
{
}
#endif

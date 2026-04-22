#if SPT_CLIENT
using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FollowerPhraseCommandPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotEventHandler), nameof(BotEventHandler.SayPhrase));
    }

    [PatchPostfix]
    private static void PatchPostfix(IPlayer player, EPhraseTrigger @event)
    {
        if (FriendlyPmcCoreFollowersPlugin.Instance is not { } plugin
            || GamePlayerOwner.MyPlayer is not { } localPlayer
            || player is null
            || !string.Equals(player.ProfileId, localPlayer.ProfileId, StringComparison.Ordinal))
        {
            return;
        }

        var command = plugin.CommandController.HandlePhrase(@event.ToString());
        if (command.HasValue)
        {
            plugin.LogPluginInfo(
                $"Follower command requested: source=phrase, trigger={@event}, command={command.Value}");
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Patches;

public sealed class FollowerPhraseCommandPatch
{
    public void Enable()
    {
    }
}
#endif

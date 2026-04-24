using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerPhraseCommandMappingPolicy
{
    public static bool TryResolve(string? phraseTriggerName, out FollowerCommand command)
    {
        switch (phraseTriggerName)
        {
            case "FollowMe":
                command = FollowerCommand.Follow;
                return true;
            case "HoldPosition":
            case "Stop":
                command = FollowerCommand.Hold;
                return true;
            case "GetInCover":
            case "CoverMe":
            case "NeedCover":
            case "TakeCover":
                command = FollowerCommand.TakeCover;
                return true;
            case "Regroup":
            case "NeedHelp":
                command = FollowerCommand.Regroup;
                return true;
            case "GoLoot":
            case "LootGeneric":
            case "LootWeapon":
            case "LootMoney":
            case "LootKey":
            case "LootBody":
            case "LootContainer":
            case "CheckHim":
                command = FollowerCommand.Loot;
                return true;
            case "Look":
                command = FollowerCommand.Attention;
                return true;
            default:
                command = default;
                return false;
        }
    }
}

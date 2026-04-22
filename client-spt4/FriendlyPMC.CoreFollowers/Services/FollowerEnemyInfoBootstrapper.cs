#if SPT_CLIENT
using Comfort.Common;
using EFT;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerEnemyInfoBootstrapper
{
    public static EnemyInfo? EnsureEnemyInfo(BotOwner botOwner, IPlayer attacker, EBotEnemyCause cause = EBotEnemyCause.addPlayerToBoss)
    {
        if (botOwner?.BotsGroup is null || botOwner.EnemiesController is null || attacker is null)
        {
            return null;
        }

        var attackerPlayer = ResolveAttackerPlayer(attacker);
        if (attackerPlayer is null)
        {
            return null;
        }

        if (botOwner.EnemiesController.EnemyInfos?.TryGetValue(attackerPlayer, out var existingInfo) == true)
        {
            existingInfo.IgnoreUntilAggression = false;
            return existingInfo;
        }

        botOwner.BotsGroup.Enemies.TryGetValue(attackerPlayer, out var groupInfo);
        if (groupInfo is null)
        {
            botOwner.BotsGroup.AddEnemy(attackerPlayer, cause);
            botOwner.BotsGroup.Enemies.TryGetValue(attackerPlayer, out groupInfo);
        }

        if (groupInfo is null)
        {
            groupInfo = new BotSettingsClass(attackerPlayer, botOwner.BotsGroup, cause);
            botOwner.Memory?.AddEnemy(attackerPlayer, groupInfo, false);
        }

        if (groupInfo is null)
        {
            return null;
        }

        groupInfo.EnemyLastPosition = attackerPlayer.Transform.position;

        var enemyInfo = botOwner.EnemiesController.AddNew(botOwner.BotsGroup, attackerPlayer, groupInfo);
        if (enemyInfo is null)
        {
            return null;
        }

        botOwner.EnemiesController.SetInfo(attackerPlayer, enemyInfo);
        enemyInfo.IgnoreUntilAggression = false;
        return enemyInfo;
    }

    private static Player? ResolveAttackerPlayer(IPlayer attacker)
    {
        if (attacker is Player player)
        {
            return player;
        }

        return Singleton<GameWorld>.Instance?.GetAlivePlayerByProfileID(attacker.ProfileId);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerEnemyInfoBootstrapper
{
}
#endif

#if SPT_CLIENT
using EFT;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerUnderFireThreatBootstrapper
{
    public static bool TryBootstrapThreat(
        BotOwner botOwner,
        IPlayer attacker,
        bool shouldSetUnderFire,
        bool shouldPromoteAttackerAsGoalEnemy,
        bool shouldMarkAttackerVisible = false,
        Vector3? awarenessPosition = null)
    {
        if (botOwner?.Memory is null || botOwner.BotsGroup is null || attacker is null)
        {
            return false;
        }

        if (shouldSetUnderFire)
        {
            botOwner.Memory.SetUnderFire(attacker);
        }

        botOwner.BotsGroup.CheckAndAddEnemy(attacker);
        botOwner.BotsGroup.ReportAboutEnemy(attacker, EEnemyPartVisibleType.NotVisible, botOwner);

        var attackerProfileId = attacker.ProfileId;
        var attackerInfo = botOwner.EnemiesController?.EnemyInfos?.Values?
            .FirstOrDefault(enemy =>
                enemy is not null
                && string.Equals(enemy.ProfileId, attackerProfileId, StringComparison.Ordinal));

        var bootstrapDecision = FollowerEnemyInfoBootstrapPolicy.Resolve(
            hasEnemyInfoAfterGroupReport: attackerInfo is not null);
        if (bootstrapDecision.ShouldCreateEnemyInfoFallback)
        {
            attackerInfo = FollowerEnemyInfoBootstrapper.EnsureEnemyInfo(botOwner, attacker);
        }

        if (shouldPromoteAttackerAsGoalEnemy && attackerInfo is not null)
        {
            botOwner.Memory.GoalEnemy = attackerInfo;
        }

        if (attackerInfo is not null)
        {
            botOwner.Memory.LastEnemy = attackerInfo;
        }

        var spottedPosition = awarenessPosition ?? attacker.Transform.position;
        botOwner.Memory.Spotted(
            byHit: shouldSetUnderFire,
            from: spottedPosition,
            secToBeSpotted: shouldMarkAttackerVisible ? 20f : 8f);

        if (shouldMarkAttackerVisible && attackerInfo is not null)
        {
            attackerInfo.SetVisible(true);
            botOwner.Memory.SetLastTimeSeeEnemy();
        }

        botOwner.Memory.AttackImmediately = shouldSetUnderFire || shouldMarkAttackerVisible;
        botOwner.CalcGoal();
        return attackerInfo is not null;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerUnderFireThreatBootstrapper
{
}
#endif

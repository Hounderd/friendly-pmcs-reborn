namespace FriendlyPMC.CoreFollowers.Services;

public static class BotDebugStateChangeDetector
{
    private const float DistanceChangeThreshold = 1.0f;

    public static IReadOnlyList<string> DescribeChanges(BotDebugSnapshot previous, BotDebugSnapshot current)
    {
        var changes = new List<string>();

        if (MathF.Abs(previous.DistanceToPlayer - current.DistanceToPlayer) >= DistanceChangeThreshold)
        {
            changes.Add("distanceToPlayer");
        }

        AddIfChanged(changes, "activeOrder", previous.ActiveOrder, current.ActiveOrder);
        AddIfChanged(changes, "activeLayer", previous.ActiveLayer, current.ActiveLayer);
        AddIfChanged(changes, "activeLogic", previous.ActiveLogic, current.ActiveLogic);
        AddIfChanged(changes, "currentRequest", previous.CurrentRequest, current.CurrentRequest);
        AddIfChanged(changes, "patrolStatus", previous.PatrolStatus, current.PatrolStatus);
        AddIfChanged(changes, "targetProfileId", previous.TargetProfileId, current.TargetProfileId);
        AddIfChanged(changes, "controlPath", previous.ControlPath, current.ControlPath);
        AddIfChanged(changes, "customBrainMode", previous.CustomBrainMode, current.CustomBrainMode);
        AddIfChanged(changes, "customNavigationIntent", previous.CustomNavigationIntent, current.CustomNavigationIntent);
        AddIfChanged(changes, "haveEnemy", previous.HaveEnemy, current.HaveEnemy);
        AddIfChanged(changes, "enemyVisible", previous.EnemyVisible, current.EnemyVisible);
        AddIfChanged(changes, "canShoot", previous.CanShoot, current.CanShoot);
        AddIfChanged(changes, "isHealing", previous.IsHealing, current.IsHealing);
        AddIfChanged(changes, "inCover", previous.InCover, current.InCover);
        AddIfChanged(changes, "isMoving", previous.IsMoving, current.IsMoving);
        AddIfChanged(changes, "isUnderFire", previous.IsUnderFire, current.IsUnderFire);
        AddIfChanged(changes, "hasActionableEnemy", previous.HasActionableEnemy, current.HasActionableEnemy);
        AddIfChanged(changes, "distanceToNearestActionableEnemy", previous.DistanceToNearestActionableEnemyMeters, current.DistanceToNearestActionableEnemyMeters);
        AddIfChanged(changes, "distanceToGoalEnemy", previous.DistanceToGoalEnemyMeters, current.DistanceToGoalEnemyMeters);
        AddIfChanged(changes, "recentThreatStimulus", previous.RecentThreatStimulus, current.RecentThreatStimulus);
        AddIfChanged(changes, "recentThreatAttackerProfileId", previous.RecentThreatAttackerProfileId, current.RecentThreatAttackerProfileId);
        AddIfChanged(changes, "followCooldownActive", previous.FollowCooldownActive, current.FollowCooldownActive);
        AddIfChanged(changes, "customMovementYieldedToCombatPressure", previous.CustomMovementYieldedToCombatPressure, current.CustomMovementYieldedToCombatPressure);
        AddIfChanged(changes, "lastCommandSummary", previous.LastCommandSummary, current.LastCommandSummary);
        AddIfChanged(changes, "lastTargetBiasSummary", previous.LastTargetBiasSummary, current.LastTargetBiasSummary);
        AddIfChanged(changes, "lastCombatAssistSummary", previous.LastCombatAssistSummary, current.LastCombatAssistSummary);
        AddIfChanged(changes, "lastCleanupSummary", previous.LastCleanupSummary, current.LastCleanupSummary);
        AddIfChanged(changes, "lastUnblockSummary", previous.LastUnblockSummary, current.LastUnblockSummary);

        return changes;
    }

    private static void AddIfChanged<T>(ICollection<string> changes, string name, T previous, T current)
    {
        if (!EqualityComparer<T>.Default.Equals(previous, current))
        {
            changes.Add(name);
        }
    }
}

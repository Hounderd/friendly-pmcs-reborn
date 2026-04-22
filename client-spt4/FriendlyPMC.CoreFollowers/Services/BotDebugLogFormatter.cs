namespace FriendlyPMC.CoreFollowers.Services;

public static class BotDebugLogFormatter
{
    private static string FormatMetric(float value)
    {
        return value >= 0f && value < float.MaxValue
            ? value.ToString("0.0")
            : "None";
    }

    private static string FormatSummary(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "None"
            : value.Replace(", ", ";").Replace(",", ";");
    }

    public static string FormatSnapshot(BotDebugSnapshot snapshot)
    {
        return string.Join(
            ", ",
            [
                $"kind={snapshot.Kind}",
                $"nick={snapshot.Nickname}",
                $"profile={snapshot.ProfileId}",
                $"role={snapshot.Role}",
                $"side={snapshot.Side}",
                $"distPlayer={snapshot.DistanceToPlayer:0.0}",
                $"distFollower={snapshot.DistanceToNearestFollower:0.0}",
                $"order={snapshot.ActiveOrder ?? "None"}",
                $"layer={snapshot.ActiveLayer ?? "None"}",
                $"logic={snapshot.ActiveLogic ?? "None"}",
                $"request={snapshot.CurrentRequest ?? "None"}",
                $"enemy={snapshot.HaveEnemy}",
                $"visible={snapshot.EnemyVisible}",
                $"canShoot={snapshot.CanShoot}",
                $"healing={snapshot.IsHealing}",
                $"cover={snapshot.InCover}",
                $"moving={snapshot.IsMoving}",
                $"patrol={snapshot.PatrolStatus ?? "None"}",
                $"target={snapshot.TargetProfileId ?? "None"}",
                $"underFire={snapshot.IsUnderFire}",
                $"goalSeen={snapshot.GoalEnemyHaveSeen}",
                $"goalAge={FormatMetric(snapshot.GoalEnemyLastSeenAgeSeconds)}",
                $"actionable={snapshot.HasActionableEnemy}",
                $"enemyDist={FormatMetric(snapshot.DistanceToNearestActionableEnemyMeters)}",
                $"goalDist={FormatMetric(snapshot.DistanceToGoalEnemyMeters)}",
                $"knownEnemies={snapshot.KnownEnemiesSummary ?? "None"}",
                $"recentThreat={snapshot.RecentThreatStimulus ?? "None"}",
                $"threatAge={FormatMetric(snapshot.RecentThreatAgeSeconds)}",
                $"threatBy={snapshot.RecentThreatAttackerProfileId ?? "None"}",
                $"control={snapshot.ControlPath ?? "None"}",
                $"customMode={snapshot.CustomBrainMode ?? "None"}",
                $"customNav={snapshot.CustomNavigationIntent ?? "None"}",
                $"followCd={snapshot.FollowCooldownActive}",
                $"followCdLeft={FormatMetric(snapshot.FollowCooldownRemainingSeconds)}",
                $"holdDist={FormatMetric(snapshot.DistanceToHoldAnchorMeters)}",
                $"cmdAge={FormatMetric(snapshot.ActiveCommandAgeSeconds)}",
                $"reqAge={FormatMetric(snapshot.CurrentRequestAgeSeconds)}",
                $"yielded={snapshot.CustomMovementYieldedToCombatPressure}",
                $"lastCmd={FormatSummary(snapshot.LastCommandSummary)}",
                $"targetBias={FormatSummary(snapshot.LastTargetBiasSummary)}",
                $"assist={FormatSummary(snapshot.LastCombatAssistSummary)}",
                $"cleanup={FormatSummary(snapshot.LastCleanupSummary)}",
                $"unblock={FormatSummary(snapshot.LastUnblockSummary)}",
            ]);
    }
}

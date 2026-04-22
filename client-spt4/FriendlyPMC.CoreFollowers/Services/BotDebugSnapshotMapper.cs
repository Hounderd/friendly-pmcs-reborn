#if SPT_CLIENT
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class BotDebugSnapshotMapper
{
    public static BotDebugWorldPoint GetWorldPoint(BotOwner botOwner)
    {
        var position = botOwner.GetPlayer?.Transform.position ?? Vector3.zero;
        return new BotDebugWorldPoint(position.x, position.y, position.z);
    }

    public static BotDebugWorldPoint GetWorldPoint(Player player)
    {
        var position = player.Transform.position;
        return new BotDebugWorldPoint(position.x, position.y, position.z);
    }

    public static BotDebugSnapshot CreateFriendlyFollowerSnapshot(
        BotOwner botOwner,
        FollowerSnapshotDto identitySnapshot,
        FollowerCommand? activeOrder,
        BotDebugWorldPoint localPlayerPosition,
        string? controlPath = null,
        string? customBrainMode = null,
        string? customNavigationIntent = null,
        bool isUnderFire = false,
        bool goalEnemyHaveSeen = false,
        float goalEnemyLastSeenAgeSeconds = -1f,
        bool hasActionableEnemy = false,
        float distanceToNearestActionableEnemyMeters = -1f,
        float distanceToGoalEnemyMeters = -1f,
        string? knownEnemiesSummary = null,
        string? recentThreatStimulus = null,
        float recentThreatAgeSeconds = -1f,
        string? recentThreatAttackerProfileId = null,
        bool followCooldownActive = false,
        float followCooldownRemainingSeconds = -1f,
        float distanceToHoldAnchorMeters = -1f,
        float activeCommandAgeSeconds = -1f,
        float currentRequestAgeSeconds = -1f,
        bool customMovementYieldedToCombatPressure = false,
        string? lastCommandSummary = null,
        string? lastTargetBiasSummary = null,
        string? lastCombatAssistSummary = null,
        string? lastCleanupSummary = null,
        string? lastUnblockSummary = null)
    {
        var worldPoint = GetWorldPoint(botOwner);
        return new BotDebugSnapshot(
            ProfileId: botOwner.ProfileId,
            Nickname: botOwner.Profile?.Info?.Nickname ?? identitySnapshot.Nickname,
            Role: botOwner.Profile?.Info?.Settings?.Role.ToString() ?? "Unknown",
            Side: botOwner.Profile?.Info?.Side.ToString() ?? identitySnapshot.Side,
            Kind: BotDebugKind.FriendlyFollower,
            DistanceToPlayer: worldPoint.DistanceTo(localPlayerPosition),
            DistanceToNearestFollower: 0f,
            ActiveOrder: activeOrder?.ToString(),
            ActiveLayer: BotDebugBrainInspector.GetActiveLayerName(botOwner),
            ActiveLogic: BotDebugBrainInspector.GetActiveLogicName(botOwner),
            CurrentRequest: botOwner.BotRequestController?.CurRequest?.BotRequestType.ToString() ?? "None",
            HaveEnemy: botOwner.Memory?.HaveEnemy == true,
            EnemyVisible: TryReadBool(botOwner.Memory?.GoalEnemy, "IsVisible"),
            CanShoot: TryReadBool(botOwner.Memory?.GoalEnemy, "CanShoot"),
            IsHealing: IsHealing(botOwner),
            InCover: TryReadBool(botOwner.Covers, "HaveCurrentCover"),
            IsMoving: TryReadBool(botOwner.Mover, "IsMoving"),
            PatrolStatus: botOwner.PatrollingData?.Status.ToString() ?? "Unknown",
            TargetProfileId: BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy),
            ControlPath: controlPath,
            CustomBrainMode: customBrainMode,
            CustomNavigationIntent: customNavigationIntent,
            IsUnderFire: isUnderFire,
            GoalEnemyHaveSeen: goalEnemyHaveSeen,
            GoalEnemyLastSeenAgeSeconds: goalEnemyLastSeenAgeSeconds,
            HasActionableEnemy: hasActionableEnemy,
            DistanceToNearestActionableEnemyMeters: distanceToNearestActionableEnemyMeters,
            DistanceToGoalEnemyMeters: distanceToGoalEnemyMeters,
            KnownEnemiesSummary: knownEnemiesSummary,
            RecentThreatStimulus: recentThreatStimulus,
            RecentThreatAgeSeconds: recentThreatAgeSeconds,
            RecentThreatAttackerProfileId: recentThreatAttackerProfileId,
            FollowCooldownActive: followCooldownActive,
            FollowCooldownRemainingSeconds: followCooldownRemainingSeconds,
            DistanceToHoldAnchorMeters: distanceToHoldAnchorMeters,
            ActiveCommandAgeSeconds: activeCommandAgeSeconds,
            CurrentRequestAgeSeconds: currentRequestAgeSeconds,
            CustomMovementYieldedToCombatPressure: customMovementYieldedToCombatPressure,
            LastCommandSummary: lastCommandSummary,
            LastTargetBiasSummary: lastTargetBiasSummary,
            LastCombatAssistSummary: lastCombatAssistSummary,
            LastCleanupSummary: lastCleanupSummary,
            LastUnblockSummary: lastUnblockSummary);
    }

    public static BotDebugSnapshot CreateComparisonSnapshot(
        BotOwner botOwner,
        BotDebugWorldPoint localPlayerPosition,
        IReadOnlyList<BotDebugWorldPoint> followerPositions)
    {
        var worldPoint = GetWorldPoint(botOwner);
        var distanceToNearestFollower = followerPositions.Count == 0
            ? float.PositiveInfinity
            : followerPositions.Min(point => point.DistanceTo(worldPoint));

        return new BotDebugSnapshot(
            ProfileId: botOwner.ProfileId,
            Nickname: botOwner.Profile?.Info?.Nickname ?? "Unknown",
            Role: botOwner.Profile?.Info?.Settings?.Role.ToString() ?? "Unknown",
            Side: botOwner.Profile?.Info?.Side.ToString() ?? "Unknown",
            Kind: BotDebugKind.ComparisonBot,
            DistanceToPlayer: worldPoint.DistanceTo(localPlayerPosition),
            DistanceToNearestFollower: distanceToNearestFollower,
            ActiveOrder: null,
            ActiveLayer: BotDebugBrainInspector.GetActiveLayerName(botOwner),
            ActiveLogic: BotDebugBrainInspector.GetActiveLogicName(botOwner),
            CurrentRequest: botOwner.BotRequestController?.CurRequest?.BotRequestType.ToString() ?? "None",
            HaveEnemy: botOwner.Memory?.HaveEnemy == true,
            EnemyVisible: TryReadBool(botOwner.Memory?.GoalEnemy, "IsVisible"),
            CanShoot: TryReadBool(botOwner.Memory?.GoalEnemy, "CanShoot"),
            IsHealing: IsHealing(botOwner),
            InCover: TryReadBool(botOwner.Covers, "HaveCurrentCover"),
            IsMoving: TryReadBool(botOwner.Mover, "IsMoving"),
            PatrolStatus: botOwner.PatrollingData?.Status.ToString() ?? "Unknown",
            TargetProfileId: BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy));
    }

    private static object? TryReadReference(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null)
        {
            return property.GetValue(instance);
        }

        var field = instance.GetType().GetField(memberName);
        return field?.GetValue(instance);
    }

    private static bool TryReadBool(object? instance, string memberName)
    {
        return TryReadReference(instance, memberName) as bool? ?? false;
    }

    private static bool IsHealing(BotOwner botOwner)
    {
        return TryReadBool(botOwner.Medecine?.FirstAid, "Using")
            || TryReadBool(botOwner.Medecine?.SurgicalKit, "Using")
            || TryReadBool(botOwner.Medecine?.Stimulators, "Using");
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class BotDebugSnapshotMapper
{
}
#endif

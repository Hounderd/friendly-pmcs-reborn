#if SPT_CLIENT
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal readonly record struct RecentPlayerShotContext(float TimeSeconds, Vector3 Origin, Vector3 Direction);

internal static class FollowerPlayerShotMemory
{
    private const float RecentShotWindowSeconds = 6f;
    private static RecentPlayerShotContext? recentShot;

    public static void RecordShot(Player player, Vector3 shotDirection)
    {
        if (player is null)
        {
            return;
        }

        var direction = shotDirection.sqrMagnitude > 0.0001f
            ? shotDirection.normalized
            : player.LookDirection.normalized;
        var origin = player.Fireport is not null
            ? player.Fireport.position
            : player.Transform.position + new Vector3(0f, 1.4f, 0f);

        recentShot = new RecentPlayerShotContext(Time.time, origin, direction);
    }

    public static bool TryGetRecentShot(out RecentPlayerShotContext context)
    {
        if (recentShot.HasValue && Time.time - recentShot.Value.TimeSeconds <= RecentShotWindowSeconds)
        {
            context = recentShot.Value;
            return true;
        }

        context = default;
        return false;
    }

    public static void Reset()
    {
        recentShot = null;
    }
}

internal readonly record struct FollowerPlayerTargetBiasResult(
    bool AppliedTargetBias,
    bool BootstrappedThreat,
    bool ShouldInterruptHealing,
    bool ShouldActivateCombatAssist,
    string? PreferredTargetProfileId,
    bool ClearedProtectedTarget);

internal static class FollowerPlayerTargetBiasRuntimeService
{
    private static readonly FieldInfo? AllAlivePlayersListField = typeof(GameWorld).GetField(
        "AllAlivePlayersList",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? RegisteredPlayersField = typeof(GameWorld).GetField(
        "RegisteredPlayers",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static FollowerPlayerTargetBiasResult TryApplyPreferredTarget(BotOwner botOwner, Player requester, FollowerCommand command)
    {
        if (botOwner is null
            || requester is null
            || command is not (FollowerCommand.Follow or FollowerCommand.Combat or FollowerCommand.TakeCover))
        {
            return default;
        }

        var memory = botOwner.Memory;
        if (memory is null)
        {
            return default;
        }

        var enemyInfos = botOwner.EnemiesController?.EnemyInfos?.Values
            ?.Where(enemy => enemy is not null && !string.IsNullOrWhiteSpace(enemy.ProfileId))
            .Select(enemy => enemy!)
            .ToArray()
            ?? Array.Empty<EnemyInfo>();

        var hasRecentShot = FollowerPlayerShotMemory.TryGetRecentShot(out var recentShot);
        var currentTargetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(memory.GoalEnemy);
        var currentTarget = new FollowerCurrentTargetState(
            currentTargetProfileId,
            IsVisible: ReadGoalEnemyBool(memory.GoalEnemy, "IsVisible"),
            CanShoot: ReadGoalEnemyBool(memory.GoalEnemy, "CanShoot"),
            IsProtected: IsProtectedTarget(botOwner, requester, currentTargetProfileId));
        var candidateArray = enemyInfos
            .Where(enemy => !IsProtectedTarget(botOwner, requester, enemy.ProfileId))
            .Select(enemy => new FollowerTargetBiasCandidate(
                enemy.ProfileId,
                hasRecentShot
                    ? ResolveAimAngleDegrees(recentShot.Origin, recentShot.Direction, enemy.CurrPosition)
                    : 180f,
                enemy.IsVisible,
                enemy.CanShoot))
            .ToArray();
        var liveShotCandidates = hasRecentShot
            ? ResolveLiveShotCandidates(botOwner, requester, recentShot).ToArray()
            : Array.Empty<LivePlayerShotCandidate>();
        var directKnownTargetProfileId = hasRecentShot
            ? ResolveShotAlignedEnemyProfileId(candidateArray)
            : null;
        var directLiveTargetProfileId = hasRecentShot
            ? FollowerPlayerShotBootstrapPolicy.ResolveTargetProfileId(
                playerHasRecentShot: true,
                liveShotCandidates.Select(candidate => candidate.BootstrapCandidate))
            : null;
        var playerIsActivelyEngaged = hasRecentShot
            || command is FollowerCommand.Combat or FollowerCommand.TakeCover
            || memory.HaveEnemy
            || candidateArray.Any(candidate => candidate.IsVisible || candidate.CanShoot);
        var preferredTargetProfileId = FollowerPlayerTargetBiasPolicy.ResolveAssistTargetProfileId(
            playerIsActivelyEngaged,
            currentTarget,
            directTargetProfileId: directKnownTargetProfileId ?? directLiveTargetProfileId,
            candidates: candidateArray);
        var sanitizationDecision = FollowerTargetBiasSanitizationPolicy.Evaluate(currentTarget, preferredTargetProfileId);
        if (sanitizationDecision.ShouldClearCurrentTarget)
        {
            if (memory.GoalEnemy?.Person is not null)
            {
                memory.DeleteInfoAboutEnemy(memory.GoalEnemy.Person);
            }

            memory.GoalEnemy = null;
            if (string.Equals(BotEnemyStateResolver.ResolveTargetProfileId(memory.LastEnemy), currentTargetProfileId, StringComparison.Ordinal))
            {
                memory.LastEnemy = null;
            }

            return new FollowerPlayerTargetBiasResult(
                AppliedTargetBias: false,
                BootstrappedThreat: false,
                ShouldInterruptHealing: false,
                ShouldActivateCombatAssist: false,
                PreferredTargetProfileId: null,
                ClearedProtectedTarget: true);
        }

        if (string.IsNullOrWhiteSpace(preferredTargetProfileId))
        {
            return default;
        }

        var preferredEnemy = enemyInfos.FirstOrDefault(enemy =>
            enemy is not null
            && string.Equals(enemy.ProfileId, preferredTargetProfileId, StringComparison.Ordinal));
        var preferredLiveTarget = liveShotCandidates.FirstOrDefault(candidate =>
            candidate.Player is not null
            && string.Equals(candidate.Player.ProfileId, preferredTargetProfileId, StringComparison.Ordinal));
        var shouldRefreshSelectedTarget = hasRecentShot
            && preferredLiveTarget.Player is not null
            && (!string.Equals(currentTargetProfileId, preferredTargetProfileId, StringComparison.Ordinal)
                || !IsHealthyTarget(currentTarget));
        var bootstrappedThreat = false;
        if (shouldRefreshSelectedTarget)
        {
            bootstrappedThreat = FollowerUnderFireThreatBootstrapper.TryBootstrapThreat(
                botOwner,
                preferredLiveTarget.Player!,
                shouldSetUnderFire: false,
                shouldPromoteAttackerAsGoalEnemy: true,
                shouldMarkAttackerVisible: true,
                awarenessPosition: preferredLiveTarget.Player!.Transform.position);
            preferredEnemy = botOwner.EnemiesController?.EnemyInfos?.Values?.FirstOrDefault(enemy =>
                enemy is not null
                && string.Equals(enemy.ProfileId, preferredTargetProfileId, StringComparison.Ordinal));
        }

        if (preferredEnemy is null)
        {
            return default;
        }

        var sameCurrentTarget = string.Equals(currentTargetProfileId, preferredTargetProfileId, StringComparison.Ordinal);
        if (sameCurrentTarget && !shouldRefreshSelectedTarget)
        {
            return default;
        }

        memory.GoalEnemy = preferredEnemy;
        memory.LastEnemy = preferredEnemy;
        return new FollowerPlayerTargetBiasResult(
            AppliedTargetBias: true,
            BootstrappedThreat: bootstrappedThreat,
            ShouldInterruptHealing: hasRecentShot,
            ShouldActivateCombatAssist: true,
            PreferredTargetProfileId: preferredTargetProfileId,
            ClearedProtectedTarget: false);
    }

    private static string? ResolveShotAlignedEnemyProfileId(IEnumerable<FollowerTargetBiasCandidate> candidates)
    {
        return FollowerPlayerTargetBiasPolicy.ResolvePreferredKnownEnemyProfileId(
            playerIsActivelyEngaged: true,
            candidates);
    }

    private static IEnumerable<LivePlayerShotCandidate> ResolveLiveShotCandidates(
        BotOwner botOwner,
        Player requester,
        RecentPlayerShotContext recentShot)
    {
        foreach (var player in EnumerateAlivePlayers())
        {
            var profileId = player?.ProfileId;
            var transform = player?.Transform;
            if (player is null
                || string.IsNullOrWhiteSpace(profileId)
                || transform is null
                || string.Equals(profileId, requester.ProfileId, StringComparison.Ordinal)
                || string.Equals(profileId, botOwner.ProfileId, StringComparison.Ordinal)
                || IsProtectedTarget(botOwner, requester, profileId))
            {
                continue;
            }

            var targetPosition = transform.position;
            yield return new LivePlayerShotCandidate(
                player,
                new FollowerPlayerShotBootstrapCandidate(
                    profileId,
                    ResolveAimAngleDegrees(recentShot.Origin, recentShot.Direction, targetPosition),
                    Vector3.Distance(recentShot.Origin, targetPosition)));
        }
    }

    private static IEnumerable<Player> EnumerateAlivePlayers()
    {
        var gameWorld = Singleton<GameWorld>.Instance;
        if (gameWorld is null)
        {
            return Array.Empty<Player>();
        }

        return ReadPlayersFromField(gameWorld, AllAlivePlayersListField)
            .Concat(ReadPlayersFromField(gameWorld, RegisteredPlayersField))
            .Where(player => player is not null)
            .GroupBy(player => player.ProfileId, StringComparer.Ordinal)
            .Select(group => group.First());
    }

    private static IEnumerable<Player> ReadPlayersFromField(GameWorld gameWorld, FieldInfo? field)
    {
        if (field?.GetValue(gameWorld) is not IEnumerable enumerable)
        {
            return Array.Empty<Player>();
        }

        return enumerable.OfType<Player>();
    }

    private static bool IsProtectedTarget(BotOwner botOwner, Player requester, string? targetProfileId)
    {
        return FollowerProtectionPolicy.ShouldProtectPlayer(
            botOwner.ProfileId,
            FriendlyPmcCoreFollowersPlugin.Instance?.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId)
                ?? Array.Empty<string>(),
            requester.ProfileId,
            targetProfileId);
    }

    private static float ResolveAimAngleDegrees(Vector3 origin, Vector3 direction, Vector3 targetPosition)
    {
        var targetVector = targetPosition - origin;
        if (targetVector.sqrMagnitude <= 0.001f || direction.sqrMagnitude <= 0.001f)
        {
            return 180f;
        }

        return Vector3.Angle(direction, targetVector.normalized);
    }

    private static bool ReadGoalEnemyBool(object? goalEnemy, string memberName)
    {
        if (goalEnemy is null)
        {
            return false;
        }

        var property = goalEnemy.GetType().GetProperty(memberName);
        if (property?.GetValue(goalEnemy) is bool propertyValue)
        {
            return propertyValue;
        }

        var field = goalEnemy.GetType().GetField(memberName);
        return field?.GetValue(goalEnemy) as bool? ?? false;
    }

    private static bool IsHealthyTarget(FollowerCurrentTargetState currentTarget)
    {
        return !currentTarget.IsProtected
            && !string.IsNullOrWhiteSpace(currentTarget.ProfileId)
            && (currentTarget.IsVisible || currentTarget.CanShoot);
    }

    private readonly record struct LivePlayerShotCandidate(
        Player? Player,
        FollowerPlayerShotBootstrapCandidate BootstrapCandidate);
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal readonly record struct FollowerPlayerTargetBiasResult(
    bool AppliedTargetBias,
    bool BootstrappedThreat,
    bool ShouldInterruptHealing,
    bool ShouldActivateCombatAssist,
    string? PreferredTargetProfileId,
    bool ClearedProtectedTarget);

internal static class FollowerPlayerShotMemory
{
    public static void Reset()
    {
    }
}

internal static class FollowerPlayerTargetBiasRuntimeService
{
}
#endif

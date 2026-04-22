#if SPT_CLIENT
using EFT;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal readonly record struct FollowerCombatPressureContext(
    bool HasActionableEnemy,
    float DistanceToNearestActionableEnemyMeters,
    bool IsUnderFire);

internal static class FollowerCombatPressureContextResolver
{
    public static FollowerCombatPressureContext Resolve(
        BotOwner botOwner,
        string? requesterProfileId,
        IEnumerable<string> registeredFollowerProfileIds,
        BotDebugWorldPoint followerPosition)
    {
        var goalEnemy = botOwner.Memory?.GoalEnemy;
        var goalEnemyProfileId = BotEnemyStateResolver.ResolveTargetProfileId(goalEnemy);
        var goalEnemyProtected = FollowerProtectionPolicy.ShouldProtectPlayer(
            botOwner.ProfileId,
            registeredFollowerProfileIds,
            requesterProfileId,
            goalEnemyProfileId);

        var knownEnemies = ResolveKnownEnemies(botOwner, requesterProfileId, registeredFollowerProfileIds, followerPosition);
        var hasActionableEnemy = FollowerActionableEnemyPolicy.HasActionableEnemy(
            new FollowerGoalEnemyState(
                HaveEnemy: botOwner.Memory?.HaveEnemy == true,
                IsVisible: ReadGoalEnemyBool(goalEnemy, "IsVisible"),
                CanShoot: ReadGoalEnemyBool(goalEnemy, "CanShoot"),
                IsProtected: goalEnemyProtected),
            knownEnemies.Select(enemy => enemy.KnownEnemyState));
        var nearestDistance = FollowerActionableEnemyDistancePolicy.ResolveNearestDistance(
            new FollowerActionableEnemyDistanceCandidate(
                IsActionable: botOwner.Memory?.HaveEnemy == true
                    && (ReadGoalEnemyBool(goalEnemy, "IsVisible") || ReadGoalEnemyBool(goalEnemy, "CanShoot"))
                    && !goalEnemyProtected,
                DistanceMeters: TryResolveGoalEnemyDistance(goalEnemy, followerPosition) ?? float.MaxValue),
            knownEnemies.Select(enemy => enemy.DistanceCandidate));

        return new FollowerCombatPressureContext(
            HasActionableEnemy: hasActionableEnemy,
            DistanceToNearestActionableEnemyMeters: nearestDistance,
            IsUnderFire: ReadBoolValue(botOwner.Memory, "IsUnderFire"));
    }

    private static IEnumerable<FollowerKnownEnemySnapshot> ResolveKnownEnemies(
        BotOwner botOwner,
        string? requesterProfileId,
        IEnumerable<string> registeredFollowerProfileIds,
        BotDebugWorldPoint followerPosition)
    {
        var enemyInfos = botOwner.EnemiesController?.EnemyInfos?.Values;
        if (enemyInfos is null)
        {
            return Array.Empty<FollowerKnownEnemySnapshot>();
        }

        return enemyInfos
            .Where(enemy => enemy is not null && !string.IsNullOrWhiteSpace(enemy.ProfileId))
            .Select(enemy => enemy!)
            .Select(enemy =>
            {
                var isProtected = FollowerProtectionPolicy.ShouldProtectPlayer(
                    botOwner.ProfileId,
                    registeredFollowerProfileIds,
                    requesterProfileId,
                    enemy.ProfileId);
                var distance = followerPosition.DistanceTo(new BotDebugWorldPoint(
                    enemy.CurrPosition.x,
                    enemy.CurrPosition.y,
                    enemy.CurrPosition.z));

                return new FollowerKnownEnemySnapshot(
                    new FollowerKnownEnemyState(
                        enemy.IsVisible,
                        enemy.CanShoot,
                        isProtected),
                    new FollowerActionableEnemyDistanceCandidate(
                        IsActionable: !isProtected && (enemy.IsVisible || enemy.CanShoot),
                        DistanceMeters: distance));
            })
            .ToArray();
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

    private static float? TryResolveGoalEnemyDistance(object? goalEnemy, BotDebugWorldPoint followerPosition)
    {
        if (goalEnemy is null)
        {
            return null;
        }

        if (TryReadVector3(goalEnemy, "CurrPosition", out var currentPosition)
            || TryReadVector3(goalEnemy, "EnemyLastPosition", out currentPosition))
        {
            return followerPosition.DistanceTo(new BotDebugWorldPoint(currentPosition.x, currentPosition.y, currentPosition.z));
        }

        var enemyPlayer = TryReadReference(goalEnemy, "Person")
            ?? TryReadReference(goalEnemy, "EnemyPlayer");
        if (enemyPlayer is not null
            && TryReadTransformPosition(enemyPlayer, out currentPosition))
        {
            return followerPosition.DistanceTo(new BotDebugWorldPoint(currentPosition.x, currentPosition.y, currentPosition.z));
        }

        return null;
    }

    private static bool TryReadVector3(object? instance, string memberName, out Vector3 vector)
    {
        vector = default;
        if (instance is null)
        {
            return false;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property?.GetValue(instance) is Vector3 propertyVector)
        {
            vector = propertyVector;
            return true;
        }

        var field = instance.GetType().GetField(memberName);
        if (field?.GetValue(instance) is Vector3 fieldVector)
        {
            vector = fieldVector;
            return true;
        }

        return false;
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

    private static bool TryReadTransformPosition(object instance, out Vector3 position)
    {
        position = default;
        var transform = TryReadReference(instance, "Transform");
        return transform is not null
            && TryReadVector3(transform, "position", out position);
    }

    private static bool ReadBoolValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return false;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property?.GetValue(instance) is bool propertyValue)
        {
            return propertyValue;
        }

        var field = instance.GetType().GetField(memberName);
        return field?.GetValue(instance) as bool? ?? false;
    }

    private readonly record struct FollowerKnownEnemySnapshot(
        FollowerKnownEnemyState KnownEnemyState,
        FollowerActionableEnemyDistanceCandidate DistanceCandidate);
}
#endif

namespace FriendlyPMC.CoreFollowers.Services;

internal static class BotEnemyStateResolver
{
    public static string? ResolveTargetProfileId(object? goalEnemy)
    {
        if (goalEnemy is null)
        {
            return null;
        }

        return TryReadReference(goalEnemy, "ProfileId") as string
            ?? TryReadReference(TryReadReference(goalEnemy, "Person"), "ProfileId") as string
            ?? TryReadReference(TryReadReference(goalEnemy, "EnemyPlayer"), "ProfileId") as string;
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
}

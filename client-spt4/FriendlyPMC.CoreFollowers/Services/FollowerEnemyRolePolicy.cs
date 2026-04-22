namespace FriendlyPMC.CoreFollowers.Services;

public enum EftWildSpawnRole
{
    UsecPmc,
    BearPmc,
    Scav,
}

public static class FollowerEnemyRolePolicy
{
    public static IReadOnlyCollection<EftWildSpawnRole> GetRequiredHostileRoles(FollowerSide side)
    {
        return side switch
        {
            FollowerSide.Bear or FollowerSide.Usec => new[]
            {
                EftWildSpawnRole.UsecPmc,
                EftWildSpawnRole.BearPmc,
                EftWildSpawnRole.Scav,
            },
            _ => new[]
            {
                EftWildSpawnRole.UsecPmc,
                EftWildSpawnRole.BearPmc,
            },
        };
    }
}

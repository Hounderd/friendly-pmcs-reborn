namespace FriendlyPMC.CoreFollowers.Services;

public enum FollowerSide
{
    Bear,
    Usec,
    Savage,
}

public enum FollowerWarnBehaviour
{
    AlwaysEnemies,
}

public sealed record FollowerMindPolicy(
    bool EnemyByGroupsPmcPlayers,
    bool EnemyByGroupsSavagePlayers,
    bool UseAddToEnemyValidation,
    bool ClearValidReasonsToAddEnemy,
    FollowerWarnBehaviour? DefaultBearBehaviour,
    FollowerWarnBehaviour? DefaultUsecBehaviour,
    FollowerWarnBehaviour? DefaultSavageBehaviour)
{
    public static FollowerMindPolicy For(FollowerSide side)
    {
        return side switch
        {
            FollowerSide.Bear => new FollowerMindPolicy(
                EnemyByGroupsPmcPlayers: true,
                EnemyByGroupsSavagePlayers: true,
                UseAddToEnemyValidation: false,
                ClearValidReasonsToAddEnemy: false,
                DefaultBearBehaviour: null,
                DefaultUsecBehaviour: FollowerWarnBehaviour.AlwaysEnemies,
                DefaultSavageBehaviour: FollowerWarnBehaviour.AlwaysEnemies),
            FollowerSide.Usec => new FollowerMindPolicy(
                EnemyByGroupsPmcPlayers: true,
                EnemyByGroupsSavagePlayers: true,
                UseAddToEnemyValidation: false,
                ClearValidReasonsToAddEnemy: false,
                DefaultBearBehaviour: FollowerWarnBehaviour.AlwaysEnemies,
                DefaultUsecBehaviour: null,
                DefaultSavageBehaviour: FollowerWarnBehaviour.AlwaysEnemies),
            _ => new FollowerMindPolicy(
                EnemyByGroupsPmcPlayers: true,
                EnemyByGroupsSavagePlayers: false,
                UseAddToEnemyValidation: false,
                ClearValidReasonsToAddEnemy: false,
                DefaultBearBehaviour: FollowerWarnBehaviour.AlwaysEnemies,
                DefaultUsecBehaviour: FollowerWarnBehaviour.AlwaysEnemies,
                DefaultSavageBehaviour: null),
        };
    }
}

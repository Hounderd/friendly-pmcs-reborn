namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerGoalEnemyState(
    bool HaveEnemy,
    bool IsVisible,
    bool CanShoot,
    bool IsProtected);

public readonly record struct FollowerKnownEnemyState(
    bool IsVisible,
    bool CanShoot,
    bool IsProtected);

public static class FollowerActionableEnemyPolicy
{
    public static bool HasActionableEnemy(
        FollowerGoalEnemyState goalEnemy,
        IEnumerable<FollowerKnownEnemyState> knownEnemies)
    {
        if (goalEnemy.HaveEnemy
            && !goalEnemy.IsProtected
            && (goalEnemy.IsVisible || goalEnemy.CanShoot))
        {
            return true;
        }

        return knownEnemies.Any(enemy =>
            !enemy.IsProtected
            && (enemy.IsVisible || enemy.CanShoot));
    }
}

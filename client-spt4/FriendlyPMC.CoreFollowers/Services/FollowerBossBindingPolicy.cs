namespace FriendlyPMC.CoreFollowers.Services;

public enum FollowerBossBindingSource
{
    None,
    Player,
    AiBossPlayer,
}

public static class FollowerBossBindingPolicy
{
    public static FollowerBossBindingSource Select(bool playerImplementsBossToFollow, bool aiBossPlayerAvailable)
    {
        if (aiBossPlayerAvailable)
        {
            return FollowerBossBindingSource.AiBossPlayer;
        }

        if (playerImplementsBossToFollow)
        {
            return FollowerBossBindingSource.Player;
        }

        return FollowerBossBindingSource.None;
    }
}

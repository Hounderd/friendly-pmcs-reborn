namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerCatchUpAuthorityMotionPlan(
    bool ShouldPausePatrolling,
    bool ShouldSprint,
    float TargetMoveSpeed);

public static class FollowerCatchUpAuthorityMotionPolicy
{
    public static FollowerCatchUpAuthorityMotionPlan Resolve()
    {
        return new FollowerCatchUpAuthorityMotionPlan(
            ShouldPausePatrolling: true,
            ShouldSprint: true,
            TargetMoveSpeed: 1f);
    }
}

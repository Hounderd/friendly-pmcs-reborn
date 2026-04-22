namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerTargetBiasSanitizationDecision(
    bool ShouldClearCurrentTarget);

public static class FollowerTargetBiasSanitizationPolicy
{
    public static FollowerTargetBiasSanitizationDecision Evaluate(
        FollowerCurrentTargetState currentTarget,
        string? preferredTargetProfileId)
    {
        var shouldClearCurrentTarget = currentTarget.IsProtected
            && !string.IsNullOrWhiteSpace(currentTarget.ProfileId)
            && string.IsNullOrWhiteSpace(preferredTargetProfileId);

        return new FollowerTargetBiasSanitizationDecision(shouldClearCurrentTarget);
    }
}

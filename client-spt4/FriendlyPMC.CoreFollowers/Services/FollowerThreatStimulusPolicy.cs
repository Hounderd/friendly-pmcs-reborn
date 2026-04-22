namespace FriendlyPMC.CoreFollowers.Services;

public enum FollowerThreatStimulusType
{
    HeardHostileShot,
    BulletNearMiss,
    HostileFootstep,
    HostileVoiceLine,
}

public readonly record struct FollowerThreatStimulusReaction(
    bool ShouldSetUnderFire,
    bool ShouldAttemptThreatBootstrap,
    bool ShouldPromoteAttackerAsGoalEnemy,
    bool ShouldMarkAttackerVisible,
    bool ShouldBreakHealing);

public static class FollowerThreatStimulusPolicy
{
    public const float DefaultHeardShotReactionDistanceMeters = 55f;
    public const float DefaultBulletNearMissReactionDistanceMeters = 50f;
    public const float DefaultHostileFootstepReactionDistanceMeters = 28f;
    public const float DefaultHostileVoiceLineReactionDistanceMeters = 60f;
    public const float DefaultImmediateVisibleFootstepDistanceMeters = 12f;
    public const float DefaultImmediateVisibleVoiceDistanceMeters = 24f;
    public const float DefaultImmediateVisibleGunshotDistanceMeters = 22f;
    public const float DefaultImmediateUnderFireGunshotDistanceMeters = 20f;
    public const float DefaultPlayerCombatSupportGunshotDistanceMeters = 42f;
    public const float DefaultPlayerCombatSupportFootstepDistanceMeters = 24f;
    public const float DefaultPlayerCombatSupportVoiceDistanceMeters = 36f;
    public const float DefaultPlayerCombatSupportVisibleGunshotDistanceMeters = 40f;
    public const float DefaultPlayerCombatSupportVisibleFootstepDistanceMeters = 20f;
    public const float DefaultPlayerCombatSupportVisibleVoiceDistanceMeters = 34f;
    public const float DefaultPlayerCombatSupportUnderFireGunshotDistanceMeters = 30f;
    public const float DefaultHealthyCurrentTargetRecentSeenSeconds = 2.5f;

    public static FollowerThreatStimulusReaction Evaluate(
        FollowerThreatStimulusType stimulusType,
        string? attackerProfileId,
        bool attackerIsProtected,
        string? currentTargetProfileId,
        bool currentTargetVisible,
        bool currentTargetCanShoot,
        float currentTargetLastSeenAgeSeconds,
        bool currentTargetIsProtected,
        bool playerIsActivelyEngaged,
        float distanceToStimulusMeters)
    {
        if (string.IsNullOrWhiteSpace(attackerProfileId) || attackerIsProtected)
        {
            return default;
        }

        var hasHealthyCurrentTarget = HasHealthyCurrentTarget(
            currentTargetProfileId,
            currentTargetVisible,
            currentTargetCanShoot,
            currentTargetLastSeenAgeSeconds,
            currentTargetIsProtected);
        if (hasHealthyCurrentTarget)
        {
            return default;
        }

        return stimulusType switch
        {
            FollowerThreatStimulusType.HeardHostileShot
                when distanceToStimulusMeters <= DefaultHeardShotReactionDistanceMeters
                => new FollowerThreatStimulusReaction(
                    ShouldSetUnderFire: distanceToStimulusMeters <= DefaultImmediateUnderFireGunshotDistanceMeters
                        || (playerIsActivelyEngaged
                            && distanceToStimulusMeters <= DefaultPlayerCombatSupportUnderFireGunshotDistanceMeters),
                    ShouldAttemptThreatBootstrap: true,
                    ShouldPromoteAttackerAsGoalEnemy: true,
                    ShouldMarkAttackerVisible: distanceToStimulusMeters <= DefaultImmediateVisibleGunshotDistanceMeters
                        || (playerIsActivelyEngaged
                            && distanceToStimulusMeters <= DefaultPlayerCombatSupportVisibleGunshotDistanceMeters),
                    ShouldBreakHealing: distanceToStimulusMeters <= DefaultImmediateVisibleGunshotDistanceMeters
                        || (playerIsActivelyEngaged
                            && distanceToStimulusMeters <= DefaultPlayerCombatSupportGunshotDistanceMeters)),
            FollowerThreatStimulusType.BulletNearMiss
                when distanceToStimulusMeters <= DefaultBulletNearMissReactionDistanceMeters
                => new FollowerThreatStimulusReaction(
                    ShouldSetUnderFire: true,
                    ShouldAttemptThreatBootstrap: true,
                    ShouldPromoteAttackerAsGoalEnemy: true,
                    ShouldMarkAttackerVisible: false,
                    ShouldBreakHealing: true),
            FollowerThreatStimulusType.HostileFootstep
                when distanceToStimulusMeters <= DefaultHostileFootstepReactionDistanceMeters
                => new FollowerThreatStimulusReaction(
                    ShouldSetUnderFire: false,
                    ShouldAttemptThreatBootstrap: true,
                    ShouldPromoteAttackerAsGoalEnemy: true,
                    ShouldMarkAttackerVisible: distanceToStimulusMeters <= DefaultImmediateVisibleFootstepDistanceMeters
                        || (playerIsActivelyEngaged
                            && distanceToStimulusMeters <= DefaultPlayerCombatSupportVisibleFootstepDistanceMeters),
                    ShouldBreakHealing: distanceToStimulusMeters <= DefaultImmediateVisibleFootstepDistanceMeters
                        || (playerIsActivelyEngaged
                            && distanceToStimulusMeters <= DefaultPlayerCombatSupportFootstepDistanceMeters)),
            FollowerThreatStimulusType.HostileVoiceLine
                when distanceToStimulusMeters <= DefaultHostileVoiceLineReactionDistanceMeters
                => new FollowerThreatStimulusReaction(
                    ShouldSetUnderFire: false,
                    ShouldAttemptThreatBootstrap: true,
                    ShouldPromoteAttackerAsGoalEnemy: true,
                    ShouldMarkAttackerVisible: distanceToStimulusMeters <= DefaultImmediateVisibleVoiceDistanceMeters
                        || (playerIsActivelyEngaged
                            && distanceToStimulusMeters <= DefaultPlayerCombatSupportVisibleVoiceDistanceMeters),
                    ShouldBreakHealing: distanceToStimulusMeters <= DefaultImmediateVisibleVoiceDistanceMeters
                        || (playerIsActivelyEngaged
                            && distanceToStimulusMeters <= DefaultPlayerCombatSupportVoiceDistanceMeters)),
            _ => default,
        };
    }

    private static bool HasHealthyCurrentTarget(
        string? currentTargetProfileId,
        bool currentTargetVisible,
        bool currentTargetCanShoot,
        float currentTargetLastSeenAgeSeconds,
        bool currentTargetIsProtected)
    {
        if (string.IsNullOrWhiteSpace(currentTargetProfileId) || currentTargetIsProtected)
        {
            return false;
        }

        if (currentTargetVisible || currentTargetCanShoot)
        {
            return true;
        }

        return currentTargetLastSeenAgeSeconds >= 0f
            && currentTargetLastSeenAgeSeconds < DefaultHealthyCurrentTargetRecentSeenSeconds;
    }
}

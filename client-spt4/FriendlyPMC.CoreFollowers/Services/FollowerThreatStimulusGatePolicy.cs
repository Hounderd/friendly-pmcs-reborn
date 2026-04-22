using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerThreatStimulusGateState(
    string? LastAttackerProfileId,
    FollowerThreatStimulusType? LastStimulusType,
    float LastAcceptedTimeSeconds);

public readonly record struct FollowerThreatStimulusGateDecision(
    bool ShouldProcessStimulus,
    FollowerThreatStimulusGateState NextState);

public static class FollowerThreatStimulusGatePolicy
{
    public const float SameAttackerAudioCueCooldownSeconds = 1.5f;
    public const float SameAttackerNearMissCooldownSeconds = 0.35f;

    public static FollowerThreatStimulusGateDecision Evaluate(
        float now,
        FollowerThreatStimulusGateState state,
        FollowerCommand command,
        bool isInFollowCombatSuppressionCooldown,
        FollowerThreatStimulusType stimulusType,
        string? attackerProfileId,
        float distanceToStimulusMeters)
    {
        if (string.IsNullOrWhiteSpace(attackerProfileId))
        {
            return new FollowerThreatStimulusGateDecision(false, state);
        }

        if (command == FollowerCommand.Follow
            && isInFollowCombatSuppressionCooldown
            && stimulusType != FollowerThreatStimulusType.BulletNearMiss
            && distanceToStimulusMeters > CustomFollowerBrainPolicy.FollowImmediateDefenseRangeMeters)
        {
            return new FollowerThreatStimulusGateDecision(false, state);
        }

        if (ShouldDeduplicate(now, state, stimulusType, attackerProfileId))
        {
            return new FollowerThreatStimulusGateDecision(false, state);
        }

        return new FollowerThreatStimulusGateDecision(
            true,
            new FollowerThreatStimulusGateState(attackerProfileId, stimulusType, now));
    }

    private static bool ShouldDeduplicate(
        float now,
        FollowerThreatStimulusGateState state,
        FollowerThreatStimulusType stimulusType,
        string attackerProfileId)
    {
        if (state.LastAcceptedTimeSeconds <= 0f
            || !string.Equals(state.LastAttackerProfileId, attackerProfileId, StringComparison.Ordinal))
        {
            return false;
        }

        var elapsedSeconds = now - state.LastAcceptedTimeSeconds;
        if (elapsedSeconds < 0f)
        {
            return false;
        }

        if (stimulusType == FollowerThreatStimulusType.BulletNearMiss
            && state.LastStimulusType == FollowerThreatStimulusType.BulletNearMiss)
        {
            return elapsedSeconds < SameAttackerNearMissCooldownSeconds;
        }

        if (IsAudioCue(stimulusType) && IsAudioCue(state.LastStimulusType))
        {
            return elapsedSeconds < SameAttackerAudioCueCooldownSeconds;
        }

        return false;
    }

    private static bool IsAudioCue(FollowerThreatStimulusType? stimulusType)
    {
        return stimulusType is FollowerThreatStimulusType.HeardHostileShot
            or FollowerThreatStimulusType.HostileFootstep
            or FollowerThreatStimulusType.HostileVoiceLine;
    }
}

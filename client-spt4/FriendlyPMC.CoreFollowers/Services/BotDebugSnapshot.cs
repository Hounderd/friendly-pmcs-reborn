namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct BotDebugWorldPoint(float X, float Y, float Z)
{
    public float DistanceTo(BotDebugWorldPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}

public enum BotDebugKind
{
    FriendlyFollower,
    ComparisonBot,
}

public readonly record struct BotDebugSelectionCandidate(
    string ProfileId,
    float DistanceToPlayer,
    float DistanceToNearestFollower)
{
    public float SortDistance => MathF.Min(DistanceToPlayer, DistanceToNearestFollower);
}

public readonly record struct BotDebugSnapshot(
    string ProfileId,
    string Nickname,
    string Role,
    string Side,
    BotDebugKind Kind,
    float DistanceToPlayer,
    float DistanceToNearestFollower,
    string? ActiveOrder,
    string? ActiveLayer,
    string? ActiveLogic,
    string? CurrentRequest,
    bool HaveEnemy,
    bool EnemyVisible,
    bool CanShoot,
    bool IsHealing,
    bool InCover,
    bool IsMoving,
    string? PatrolStatus,
    string? TargetProfileId,
    string? ControlPath = null,
    string? CustomBrainMode = null,
    string? CustomNavigationIntent = null,
    bool IsUnderFire = false,
    bool GoalEnemyHaveSeen = false,
    float GoalEnemyLastSeenAgeSeconds = -1f,
    bool HasActionableEnemy = false,
    float DistanceToNearestActionableEnemyMeters = -1f,
    float DistanceToGoalEnemyMeters = -1f,
    string? KnownEnemiesSummary = null,
    string? RecentThreatStimulus = null,
    float RecentThreatAgeSeconds = -1f,
    string? RecentThreatAttackerProfileId = null,
    bool FollowCooldownActive = false,
    float FollowCooldownRemainingSeconds = -1f,
    float DistanceToHoldAnchorMeters = -1f,
    float ActiveCommandAgeSeconds = -1f,
    float CurrentRequestAgeSeconds = -1f,
    bool CustomMovementYieldedToCombatPressure = false,
    string? LastCommandSummary = null,
    string? LastTargetBiasSummary = null,
    string? LastCombatAssistSummary = null,
    string? LastCleanupSummary = null,
    string? LastUnblockSummary = null);

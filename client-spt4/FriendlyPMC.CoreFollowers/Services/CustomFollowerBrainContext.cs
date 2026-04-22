using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerBrainContext(
    FollowerCommand Command,
    CustomFollowerBrainMode CurrentMode,
    float DistanceToPlayerMeters,
    float DistanceToHoldAnchorMeters,
    bool HasActionableEnemy,
    float DistanceToNearestActionableEnemyMeters,
    float DistanceToGoalEnemyMeters,
    bool IsUnderFire,
    bool HasPreferredTarget,
    bool HasRecentCombatPressure,
    bool IsNavigationStuck,
    bool IsInFollowCombatSuppressionCooldown,
    FollowerModeSettings Settings);

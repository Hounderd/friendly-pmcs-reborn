using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerMovementOffset(float X, float Y, float Z);

public static class FollowerOrderLayerPolicy
{
    public static bool HasActionableEnemy(bool haveEnemy, bool haveProtectedTarget = false)
    {
        return haveEnemy && !haveProtectedTarget;
    }

    public static bool ShouldActivateMovementLayer(
        FollowerCommand command,
        bool haveEnemy,
        bool haveProtectedTarget = false,
        float distanceToPlayerMeters = 0f,
        float distanceToHoldAnchorMeters = 0f,
        FollowerModeSettings? settings = null)
    {
        var effectiveSettings = settings ?? new FollowerModeSettings();
        var disposition = FollowerModePolicy.ResolveDisposition(
            command,
            HasActionableEnemy(haveEnemy, haveProtectedTarget),
            distanceToPlayerMeters,
            distanceToHoldAnchorMeters,
            effectiveSettings);

        return disposition is FollowerModeDisposition.ForceFollowMovement
            or FollowerModeDisposition.ForceCatchUpMovement
            or FollowerModeDisposition.ForceReturnToCombatRange;
    }

    public static FollowerMovementOffset GetOffset(FollowerCommand command)
    {
        return GetOffset(command, FollowerMovementIntent.MoveToFormation);
    }

    public static FollowerMovementOffset GetOffset(FollowerCommand command, FollowerMovementIntent intent)
    {
        return (command, intent) switch
        {
            (_, FollowerMovementIntent.CatchUpToPlayer) => new FollowerMovementOffset(0f, 0f, 0f),
            (FollowerCommand.Combat, FollowerMovementIntent.ReturnToCombatRange) => new FollowerMovementOffset(0f, 0f, -10f),
            (FollowerCommand.Regroup, _) => new FollowerMovementOffset(0f, 0f, 0f),
            _ => new FollowerMovementOffset(4.5f, 0f, -7f),
        };
    }
}

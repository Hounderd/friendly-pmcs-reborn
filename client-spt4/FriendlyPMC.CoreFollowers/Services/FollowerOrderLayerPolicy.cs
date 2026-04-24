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
        return GetOffset(command, intent, formationSlotIndex: 0);
    }

    public static FollowerMovementOffset GetOffset(
        FollowerCommand command,
        FollowerMovementIntent intent,
        int formationSlotIndex)
    {
        return (command, intent) switch
        {
            (FollowerCommand.Follow, FollowerMovementIntent.CatchUpToPlayer) => ResolveFormationOffset(formationSlotIndex),
            (FollowerCommand.Combat, FollowerMovementIntent.ReturnToCombatRange) => new FollowerMovementOffset(0f, 0f, -10f),
            (FollowerCommand.Regroup, _) => new FollowerMovementOffset(0f, 0f, 0f),
            (_, FollowerMovementIntent.CatchUpToPlayer) => ResolveFormationOffset(formationSlotIndex),
            _ => ResolveFormationOffset(formationSlotIndex),
        };
    }

    private static FollowerMovementOffset ResolveFormationOffset(int formationSlotIndex)
    {
        var normalizedSlot = Math.Max(0, formationSlotIndex);
        var row = normalizedSlot / 2;
        var side = normalizedSlot % 2 == 0 ? 1f : -1f;
        var lateral = 4.5f + (row * 2.5f);
        var rear = -7f - (row * 3f);

        return new FollowerMovementOffset(side * lateral, 0f, rear);
    }
}

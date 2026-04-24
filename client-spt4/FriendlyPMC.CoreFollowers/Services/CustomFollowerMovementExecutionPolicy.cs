using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerMovementExecutionPlan(
    bool ShouldMove,
    FollowerMovementIntent MovementIntent,
    bool ShouldSprint,
    float ArrivalRadiusMeters,
    bool ForcePathRefresh);

public static class CustomFollowerMovementExecutionPolicy
{
    private const float FormationSettleSlackMeters = 3.5f;
    private const float NearPlayerIdleBandMeters = 5f;
    private const float AlwaysSprintDistanceMeters = 30f;

    public static CustomFollowerMovementExecutionPlan Resolve(
        FollowerCommand command,
        CustomFollowerNavigationIntent navigationIntent,
        float distanceToPlayerMeters,
        FollowerModeSettings settings)
    {
        return navigationIntent switch
        {
            CustomFollowerNavigationIntent.MoveToFormation => ResolveFormation(distanceToPlayerMeters, settings),
            CustomFollowerNavigationIntent.CatchUpToPlayer => new CustomFollowerMovementExecutionPlan(
                ShouldMove: true,
                MovementIntent: FollowerMovementIntent.CatchUpToPlayer,
                ShouldSprint: true,
                ArrivalRadiusMeters: MathF.Max(settings.FollowDeadzoneMeters, 2.5f),
                ForcePathRefresh: false),
            CustomFollowerNavigationIntent.ReturnToCombatRange => new CustomFollowerMovementExecutionPlan(
                ShouldMove: true,
                MovementIntent: FollowerMovementIntent.ReturnToCombatRange,
                ShouldSprint: true,
                ArrivalRadiusMeters: MathF.Max(settings.FollowDeadzoneMeters, 3f),
                ForcePathRefresh: false),
            CustomFollowerNavigationIntent.RepathAndRecover => ResolveRecovery(command, settings),
            _ => new CustomFollowerMovementExecutionPlan(
                ShouldMove: false,
                MovementIntent: FollowerMovementIntent.HoldFormation,
                ShouldSprint: false,
                ArrivalRadiusMeters: settings.FollowDeadzoneMeters,
                ForcePathRefresh: false),
        };
    }

    private static CustomFollowerMovementExecutionPlan ResolveFormation(
        float distanceToPlayerMeters,
        FollowerModeSettings settings)
    {
        var formationDistance = GetIdealFormationDistance();
        var formationMinDistance = MathF.Max(settings.FollowDeadzoneMeters, formationDistance - FormationSettleSlackMeters);
        var formationMaxDistance = formationDistance + FormationSettleSlackMeters;
        var stableHoldDistance = MathF.Max(
            settings.FollowDeadzoneMeters,
            FollowerCatchUpPolicy.StableFollowHoldDistanceMeters);

        if (distanceToPlayerMeters <= NearPlayerIdleBandMeters
            || distanceToPlayerMeters <= stableHoldDistance
            || (distanceToPlayerMeters >= formationMinDistance && distanceToPlayerMeters <= formationMaxDistance))
        {
            return new CustomFollowerMovementExecutionPlan(
                ShouldMove: false,
                MovementIntent: FollowerMovementIntent.HoldFormation,
                ShouldSprint: false,
                ArrivalRadiusMeters: settings.FollowDeadzoneMeters,
                ForcePathRefresh: false);
        }

        return new CustomFollowerMovementExecutionPlan(
            ShouldMove: true,
            MovementIntent: FollowerMovementIntent.MoveToFormation,
            ShouldSprint: distanceToPlayerMeters >= AlwaysSprintDistanceMeters
                || distanceToPlayerMeters >= settings.CatchUpDistanceMeters,
            ArrivalRadiusMeters: MathF.Max(settings.FollowDeadzoneMeters, 2.5f),
            ForcePathRefresh: false);
    }

    private static float GetIdealFormationDistance()
    {
        var offset = FollowerOrderLayerPolicy.GetOffset(FollowerCommand.Follow, FollowerMovementIntent.MoveToFormation);
        return MathF.Sqrt((offset.X * offset.X) + (offset.Z * offset.Z));
    }

    private static CustomFollowerMovementExecutionPlan ResolveRecovery(
        FollowerCommand command,
        FollowerModeSettings settings)
    {
        var movementIntent = command == FollowerCommand.Combat
            ? FollowerMovementIntent.ReturnToCombatRange
            : FollowerMovementIntent.CatchUpToPlayer;

        return new CustomFollowerMovementExecutionPlan(
            ShouldMove: true,
            MovementIntent: movementIntent,
            ShouldSprint: true,
            ArrivalRadiusMeters: MathF.Max(settings.FollowDeadzoneMeters, 2.5f),
            ForcePathRefresh: true);
    }
}

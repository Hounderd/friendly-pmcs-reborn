namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerFormationTargetState(
    BotDebugWorldPoint PlayerPosition,
    BotDebugWorldPoint TargetPoint,
    bool HasValue);

public readonly record struct CustomFollowerFormationTargetResult(
    BotDebugWorldPoint TargetPoint,
    CustomFollowerFormationTargetState NextState);

public static class CustomFollowerFormationTargetPolicy
{
    private const float MinimumPlayerShiftMeters = 1.25f;

    public static CustomFollowerFormationTargetResult Resolve(
        CustomFollowerFormationTargetState state,
        BotDebugWorldPoint currentPlayerPosition,
        BotDebugWorldPoint desiredTargetPoint)
    {
        if (state.HasValue
            && state.PlayerPosition.DistanceTo(currentPlayerPosition) < MinimumPlayerShiftMeters)
        {
            return new CustomFollowerFormationTargetResult(
                state.TargetPoint,
                state);
        }

        return new CustomFollowerFormationTargetResult(
            desiredTargetPoint,
            new CustomFollowerFormationTargetState(
                currentPlayerPosition,
                desiredTargetPoint,
                HasValue: true));
    }
}

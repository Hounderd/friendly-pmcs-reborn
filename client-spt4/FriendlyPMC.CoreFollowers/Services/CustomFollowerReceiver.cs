using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed class CustomFollowerReceiver
{
    public CustomFollowerReceiver()
    {
        CurrentState = new CustomFollowerReceiverState(FollowerCommand.Follow);
    }

    public CustomFollowerReceiverState CurrentState { get; private set; }

    public BotDebugWorldPoint? HoldAnchor { get; private set; }

    public float LastFollowCommandTimestamp { get; private set; }

    public void SetCommand(FollowerCommand command)
    {
        if (command == FollowerCommand.Follow)
        {
            LastFollowCommandTimestamp = GetCurrentTime();
        }

        CurrentState = (command == FollowerCommand.Hold || command == FollowerCommand.TakeCover) && HoldAnchor.HasValue
            ? new CustomFollowerReceiverState(command, HasHoldAnchor: true)
            : new CustomFollowerReceiverState(command, HasHoldAnchor: false);
    }

    public void SetHoldAnchor(BotDebugWorldPoint anchor)
    {
        HoldAnchor = anchor;
        var anchoredCommand = CurrentState.Command == FollowerCommand.TakeCover
            ? FollowerCommand.TakeCover
            : FollowerCommand.Hold;
        CurrentState = new CustomFollowerReceiverState(anchoredCommand, HasHoldAnchor: true);
    }

    public bool IsInFollowCombatSuppressionCooldown(float now)
    {
        if (CurrentState.Command != FollowerCommand.Follow || LastFollowCommandTimestamp <= 0f)
        {
            return false;
        }

        return now - LastFollowCommandTimestamp < CustomFollowerBrainPolicy.FollowCombatSuppressionCooldownSeconds;
    }

    internal static Func<float> TimeProvider { get; set; } = () => 0f;

    private static float GetCurrentTime()
    {
#if SPT_CLIENT
        return UnityEngine.Time.time;
#else
        return TimeProvider();
#endif
    }
}

using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public sealed class FollowerCommandBindings
{
    private readonly Dictionary<string, FollowerCommand> commandByKey;

    public FollowerCommandBindings(string followKey, string holdKey, string combatKey, string? healKey = null)
    {
        commandByKey = new Dictionary<string, FollowerCommand>(StringComparer.OrdinalIgnoreCase)
        {
            [followKey] = FollowerCommand.Follow,
            [holdKey] = FollowerCommand.Hold,
            [combatKey] = FollowerCommand.Combat,
        };

        if (!string.IsNullOrWhiteSpace(healKey))
        {
            commandByKey[healKey] = FollowerCommand.Heal;
        }
    }

    public FollowerCommand? ResolvePressedCommand(string keyPressed)
    {
        if (commandByKey.TryGetValue(keyPressed, out var command))
        {
            return command;
        }

        return null;
    }
}

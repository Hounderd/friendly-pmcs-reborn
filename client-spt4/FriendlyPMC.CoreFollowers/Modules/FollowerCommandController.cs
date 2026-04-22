using FriendlyPMC.CoreFollowers.Models;
using FriendlyPMC.CoreFollowers.Services;

namespace FriendlyPMC.CoreFollowers.Modules;

public interface IFollowerRuntimeHandle
{
    string Aid { get; }

    string RuntimeProfileId { get; }

    bool IsOperational { get; }

    int HealthPercent { get; }

    BotDebugWorldPoint CurrentPosition { get; }

    BotDebugWorldPoint GetPlateAnchorPoint();

    FollowerSnapshotDto CaptureSnapshot();

    BotDebugSnapshot CaptureDebugSnapshot(FollowerCommand? activeOrder, BotDebugWorldPoint localPlayerPosition);

    void Execute(FollowerCommand command);

    void TickOrder();
}

public sealed class FollowerCommandController
{
    private readonly FollowerRegistry registry;
    private readonly FollowerCommandBindings bindings;

    public FollowerCommandController(FollowerRegistry registry, FollowerCommandBindings bindings)
    {
        this.registry = registry;
        this.bindings = bindings;
    }

    public FollowerCommand? HandleKeyPress(string keyPressed)
    {
        var command = bindings.ResolvePressedCommand(keyPressed);
        if (!command.HasValue)
        {
            return null;
        }

        IssueCommand(command.Value);
        return command;
    }

    public FollowerCommand? HandlePhrase(string phraseTriggerName)
    {
        if (!FollowerPhraseCommandMappingPolicy.TryResolve(phraseTriggerName, out var command))
        {
            return null;
        }

        IssueCommand(command);
        return command;
    }

    private void IssueCommand(FollowerCommand command)
    {
        foreach (var follower in registry.RuntimeFollowers)
        {
            if (command is not (FollowerCommand.Attention or FollowerCommand.Heal or FollowerCommand.Loot))
            {
                registry.SetActiveOrder(follower.Aid, command);
                if (registry.TryGetCustomBrainSession(follower.Aid, out var session))
                {
                    session.ApplyCommand(command, follower.CurrentPosition);
                }
            }

            follower.Execute(command);
        }
    }

    public int ActiveFollowerCount => registry.Followers.Count;
}

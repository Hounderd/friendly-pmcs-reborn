using FriendlyPMC.Server.Models;
using SPTarkov.DI.Annotations;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerCommandStateService
{
    private readonly Dictionary<string, FollowerCommandState> stateBySession = new();

    public FollowerCommandState Get(string sessionId)
    {
        if (stateBySession.TryGetValue(sessionId, out var state))
        {
            return state;
        }

        return new FollowerCommandState(FollowerCommandMode.Follow);
    }

    public void Set(string sessionId, FollowerCommandMode mode)
    {
        stateBySession[sessionId] = new FollowerCommandState(mode);
    }
}

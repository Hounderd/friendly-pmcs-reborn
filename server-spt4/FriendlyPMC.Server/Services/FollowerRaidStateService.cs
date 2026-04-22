using FriendlyPMC.Server.Models;
using SPTarkov.DI.Annotations;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerRaidStateService
{
    public IReadOnlyList<FollowerProfileSnapshot> PrepareForRaid(IReadOnlyList<FollowerProfileSnapshot> savedFollowers)
    {
        return savedFollowers
            .Select(follower => follower with
            {
                Health = follower.Health.WithAllPartsHealed(),
            })
            .ToArray();
    }
}

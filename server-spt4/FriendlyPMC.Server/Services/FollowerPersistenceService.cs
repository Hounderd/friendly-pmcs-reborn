using FriendlyPMC.Server.Models;
using SPTarkov.DI.Annotations;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerPersistenceService
{
    private readonly FollowerRosterStore store;

    public FollowerPersistenceService(FollowerRosterStore store)
    {
        this.store = store;
    }

    public Task<IReadOnlyList<FollowerProfileSnapshot>> LoadPersistedFollowersAsync(string sessionId)
    {
        return store.LoadProfilesAsync(sessionId);
    }

    public async Task SaveRaidProgressAsync(string sessionId, IReadOnlyList<FollowerProfileSnapshot> followers)
    {
        await store.SaveProfilesAsync(sessionId, followers);

        var roster = followers
            .Select(follower => new FollowerRosterRecord(follower.Aid, follower.Nickname, follower.Side))
            .ToArray();

        await store.SaveRosterAsync(sessionId, roster);
    }

    public async Task RegisterRecruitAsync(string sessionId, FollowerRosterRecord follower)
    {
        var roster = (await store.LoadRosterAsync(sessionId)).ToList();
        if (roster.All(existing => existing.Aid != follower.Aid))
        {
            roster.Add(follower);
            await store.SaveRosterAsync(sessionId, roster);
        }
    }
}

using FriendlyPMC.CoreFollowers.Models;
using FriendlyPMC.CoreFollowers.Services;

namespace FriendlyPMC.CoreFollowers.Modules;

public sealed class FollowerRaidController
{
    private readonly IFollowerApiClient apiClient;
    private readonly FollowerRegistry registry;
    private readonly Action<string>? logInfo;
    private readonly List<FollowerSnapshotDto> pendingFollowers = new();
    private readonly HashSet<string> raidStartFollowerAids = new(StringComparer.Ordinal);
    private readonly HashSet<string> spawnedFollowerAids = new(StringComparer.Ordinal);
    private bool raidFollowerSpawnStarted;

    public FollowerRaidController(IFollowerApiClient apiClient, FollowerRegistry registry, Action<string>? logInfo = null)
    {
        this.apiClient = apiClient;
        this.registry = registry;
        this.logInfo = logInfo;
    }

    public IReadOnlyList<FollowerSnapshotDto> PendingFollowers => pendingFollowers;

    public bool HasPendingFollowers => pendingFollowers.Count > 0;

    public async Task PrimeRaidFollowersAsync()
    {
        pendingFollowers.Clear();
        raidStartFollowerAids.Clear();
        spawnedFollowerAids.Clear();

        var activeFollowers = await apiClient.GetActiveFollowersAsync();
        pendingFollowers.AddRange(activeFollowers);
        foreach (var follower in activeFollowers)
        {
            raidStartFollowerAids.Add(follower.Aid);
        }

        raidFollowerSpawnStarted = false;
    }

    public void SetActiveFollowers(IReadOnlyList<FollowerSnapshotDto> followers)
    {
        pendingFollowers.Clear();
        pendingFollowers.AddRange(followers);
        raidStartFollowerAids.Clear();
        spawnedFollowerAids.Clear();
        foreach (var follower in followers)
        {
            raidStartFollowerAids.Add(follower.Aid);
        }

        raidFollowerSpawnStarted = false;
    }

    public void AttachSpawnedFollower(FollowerSnapshotDto follower)
    {
        pendingFollowers.RemoveAll(existing => existing.Aid == follower.Aid);
        spawnedFollowerAids.Add(follower.Aid);
    }

    public bool TryBeginPendingFollowerSpawn()
    {
        if (raidFollowerSpawnStarted || pendingFollowers.Count == 0)
        {
            return false;
        }

        raidFollowerSpawnStarted = true;
        return true;
    }

    public void AllowPendingFollowerSpawnRetry()
    {
        raidFollowerSpawnStarted = false;
    }

    public bool TryAttachPendingFollower(Func<FollowerSnapshotDto, IFollowerRuntimeHandle> followerFactory)
    {
        if (pendingFollowers.Count == 0)
        {
            return false;
        }

        var pendingFollower = pendingFollowers[0];
        var runtimeFollower = followerFactory(pendingFollower);
        registry.RegisterRuntime(runtimeFollower);
        pendingFollowers.RemoveAt(0);
        spawnedFollowerAids.Add(pendingFollower.Aid);
        return true;
    }

    public Task SaveRaidProgressAsync()
    {
        var payloadFollowers = CreateRaidProgressPayload();
        foreach (var delta in registry.DescribeInventoryChanges(payloadFollowers))
        {
            logInfo?.Invoke(
                $"Follower inventory delta: follower={delta.Nickname}, aid={delta.Aid}, startItems={delta.InitialItemCount}, endItems={delta.CurrentItemCount}, added={delta.AddedCount}, removed={delta.RemovedCount}");
        }

        return apiClient.SaveRaidProgressAsync(
            new FollowerRaidProgressPayload(
                payloadFollowers,
                raidStartFollowerAids.ToArray(),
                spawnedFollowerAids.ToArray(),
                registry.GetKnownDeadFollowerAids(spawnedFollowerAids)));
    }

    public void Reset()
    {
        pendingFollowers.Clear();
        raidStartFollowerAids.Clear();
        spawnedFollowerAids.Clear();
        raidFollowerSpawnStarted = false;
        registry.Clear();
    }

    private IReadOnlyList<FollowerSnapshotDto> CreateRaidProgressPayload()
    {
        var runtimeSnapshots = registry.CreateRaidProgressPayload();
        if (pendingFollowers.Count == 0)
        {
            return runtimeSnapshots;
        }

        var payload = new List<FollowerSnapshotDto>(runtimeSnapshots);
        var seenAids = new HashSet<string>(runtimeSnapshots.Select(snapshot => snapshot.Aid), StringComparer.Ordinal);
        foreach (var pendingFollower in pendingFollowers)
        {
            if (seenAids.Add(pendingFollower.Aid))
            {
                payload.Add(pendingFollower);
            }
        }

        return payload;
    }
}

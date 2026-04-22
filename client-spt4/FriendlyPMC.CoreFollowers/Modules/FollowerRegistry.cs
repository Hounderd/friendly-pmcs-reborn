using FriendlyPMC.CoreFollowers.Models;
using FriendlyPMC.CoreFollowers.Services;
namespace FriendlyPMC.CoreFollowers.Modules;

public sealed class FollowerRegistry
{
    private readonly Dictionary<string, FollowerSnapshotDto> followers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FollowerSnapshotDto> initialFollowers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IFollowerRuntimeHandle> runtimeFollowers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> aidByRuntimeProfileId = new(StringComparer.Ordinal);
    private readonly HashSet<string> expectedRuntimeProfileIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> nonOperationalFollowerAids = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FollowerCommand> activeOrders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BotDebugWorldPoint> holdAnchors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustomFollowerControlPathRuntime> controlPathRuntimes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustomFollowerBrainRuntimeSession> customBrainSessions = new(StringComparer.Ordinal);

    public IReadOnlyList<FollowerSnapshotDto> Followers => followers.Values.ToArray();

    public IReadOnlyCollection<IFollowerRuntimeHandle> RuntimeFollowers => runtimeFollowers.Values;

    public bool Contains(string aid)
    {
        return followers.ContainsKey(aid);
    }

    public void Register(FollowerSnapshotDto follower)
    {
        followers[follower.Aid] = follower;
        initialFollowers.TryAdd(follower.Aid, follower);
    }

    public void RegisterRuntime(IFollowerRuntimeHandle follower)
    {
        if (aidByRuntimeProfileId.TryGetValue(follower.RuntimeProfileId, out var existingAid)
            && !string.Equals(existingAid, follower.Aid, StringComparison.Ordinal))
        {
            runtimeFollowers.Remove(existingAid);
        }

        expectedRuntimeProfileIds.Remove(follower.RuntimeProfileId);
        runtimeFollowers[follower.Aid] = follower;
        aidByRuntimeProfileId[follower.RuntimeProfileId] = follower.Aid;
        if (follower.IsOperational)
        {
            nonOperationalFollowerAids.Remove(follower.Aid);
        }
        var snapshot = follower.CaptureSnapshot();
        followers[follower.Aid] = snapshot;
        initialFollowers.TryAdd(follower.Aid, snapshot);
        activeOrders.TryAdd(follower.Aid, FollowerCommand.Follow);
    }

    public void MarkExpectedRuntimeProfileId(string runtimeProfileId)
    {
        if (string.IsNullOrWhiteSpace(runtimeProfileId))
        {
            return;
        }

        expectedRuntimeProfileIds.Add(runtimeProfileId);
    }

    public void UnmarkExpectedRuntimeProfileId(string runtimeProfileId)
    {
        if (string.IsNullOrWhiteSpace(runtimeProfileId))
        {
            return;
        }

        expectedRuntimeProfileIds.Remove(runtimeProfileId);
    }

    public bool IsManagedRuntimeProfileId(string runtimeProfileId)
    {
        if (string.IsNullOrWhiteSpace(runtimeProfileId))
        {
            return false;
        }

        return aidByRuntimeProfileId.ContainsKey(runtimeProfileId)
            || expectedRuntimeProfileIds.Contains(runtimeProfileId);
    }

    public bool TryGetRuntime(string aid, out IFollowerRuntimeHandle follower)
    {
        return runtimeFollowers.TryGetValue(aid, out follower!);
    }

    public bool RemoveRuntime(string aid)
    {
        if (runtimeFollowers.TryGetValue(aid, out var runtimeFollower))
        {
            aidByRuntimeProfileId.Remove(runtimeFollower.RuntimeProfileId);
            if (!runtimeFollower.IsOperational)
            {
                nonOperationalFollowerAids.Add(aid);
            }
        }

        activeOrders.Remove(aid);
        holdAnchors.Remove(aid);
        controlPathRuntimes.Remove(aid);
        customBrainSessions.Remove(aid);
        return runtimeFollowers.Remove(aid);
    }

    public IReadOnlyList<string> GetKnownDeadFollowerAids(IReadOnlyCollection<string>? candidateAids = null)
    {
        if (candidateAids is null || candidateAids.Count == 0)
        {
            return nonOperationalFollowerAids.ToArray();
        }

        return candidateAids
            .Where(aid => nonOperationalFollowerAids.Contains(aid))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public void SetActiveOrder(string aid, FollowerCommand command)
    {
        activeOrders[aid] = command;
    }

    public bool TryGetActiveOrder(string aid, out FollowerCommand command)
    {
        return activeOrders.TryGetValue(aid, out command);
    }

    public bool TryGetRuntimeByProfileId(string runtimeProfileId, out IFollowerRuntimeHandle follower)
    {
        if (aidByRuntimeProfileId.TryGetValue(runtimeProfileId, out var aid)
            && runtimeFollowers.TryGetValue(aid, out var resolvedFollower))
        {
            follower = resolvedFollower;
            return true;
        }

        follower = null!;
        return false;
    }

    public bool TryGetActiveOrderByProfileId(string runtimeProfileId, out FollowerCommand command)
    {
        command = default;
        return aidByRuntimeProfileId.TryGetValue(runtimeProfileId, out var aid)
            && activeOrders.TryGetValue(aid, out command);
    }

    public void SetHoldAnchor(string aid, BotDebugWorldPoint anchor)
    {
        holdAnchors[aid] = anchor;
    }

    public bool TryGetHoldAnchor(string aid, out BotDebugWorldPoint anchor)
    {
        return holdAnchors.TryGetValue(aid, out anchor);
    }

    public void SetControlPathRuntime(string aid, CustomFollowerControlPathRuntime runtime)
    {
        controlPathRuntimes[aid] = runtime;
    }

    public bool TryGetControlPathRuntime(string aid, out CustomFollowerControlPathRuntime runtime)
    {
        return controlPathRuntimes.TryGetValue(aid, out runtime);
    }

    public bool TryGetControlPathRuntimeByProfileId(string runtimeProfileId, out CustomFollowerControlPathRuntime runtime)
    {
        runtime = default;
        return aidByRuntimeProfileId.TryGetValue(runtimeProfileId, out var aid)
            && controlPathRuntimes.TryGetValue(aid, out runtime);
    }

    public void SetCustomBrainSession(string aid, CustomFollowerBrainRuntimeSession session)
    {
        customBrainSessions[aid] = session;
    }

    public bool TryGetCustomBrainSession(string aid, out CustomFollowerBrainRuntimeSession session)
    {
        return customBrainSessions.TryGetValue(aid, out session!);
    }

    public bool TryGetCustomBrainSessionByProfileId(string runtimeProfileId, out CustomFollowerBrainRuntimeSession session)
    {
        if (aidByRuntimeProfileId.TryGetValue(runtimeProfileId, out var aid)
            && customBrainSessions.TryGetValue(aid, out var resolvedSession))
        {
            session = resolvedSession;
            return true;
        }

        session = null!;
        return false;
    }

    public IReadOnlyList<FollowerSnapshotDto> CreateRaidProgressPayload()
    {
        foreach (var runtimeFollower in runtimeFollowers.Values)
        {
            TryRefreshStoredSnapshot(runtimeFollower);
        }

        var payload = runtimeFollowers.Values
            .Where(follower => follower.IsOperational)
            .Select(follower => followers[follower.Aid])
            .ToList();
        var seenAids = new HashSet<string>(payload.Select(follower => follower.Aid), StringComparer.Ordinal);

        foreach (var follower in followers.Values)
        {
            if (seenAids.Add(follower.Aid))
            {
                payload.Add(follower);
            }
        }

        return payload;
    }

    public IReadOnlyList<FollowerInventoryDelta> DescribeInventoryChanges(IReadOnlyList<FollowerSnapshotDto> payload)
    {
        var deltas = new List<FollowerInventoryDelta>(payload.Count);
        foreach (var follower in payload)
        {
            if (!initialFollowers.TryGetValue(follower.Aid, out var initialFollower))
            {
                continue;
            }

            var initialIds = initialFollower.InventoryItemIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            var currentIds = follower.InventoryItemIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            var addedCount = currentIds.Count(id => !initialIds.Contains(id));
            var removedCount = initialIds.Count(id => !currentIds.Contains(id));
            if (addedCount == 0 && removedCount == 0)
            {
                continue;
            }

            deltas.Add(new FollowerInventoryDelta(
                follower.Aid,
                follower.Nickname,
                initialIds.Count,
                currentIds.Count,
                addedCount,
                removedCount));
        }

        return deltas;
    }

    private void TryRefreshStoredSnapshot(IFollowerRuntimeHandle runtimeFollower)
    {
        try
        {
            if (!runtimeFollower.IsOperational)
            {
                nonOperationalFollowerAids.Add(runtimeFollower.Aid);
            }
            else
            {
                nonOperationalFollowerAids.Remove(runtimeFollower.Aid);
            }

            followers[runtimeFollower.Aid] = runtimeFollower.CaptureSnapshot();
        }
        catch
        {
            // Preserve the last known stored snapshot when runtime capture fails during teardown.
        }
    }

    public void Clear()
    {
        followers.Clear();
        initialFollowers.Clear();
        runtimeFollowers.Clear();
        aidByRuntimeProfileId.Clear();
        expectedRuntimeProfileIds.Clear();
        nonOperationalFollowerAids.Clear();
        activeOrders.Clear();
        holdAnchors.Clear();
        controlPathRuntimes.Clear();
        customBrainSessions.Clear();
    }
}

public readonly record struct FollowerInventoryDelta(
    string Aid,
    string Nickname,
    int InitialItemCount,
    int CurrentItemCount,
    int AddedCount,
    int RemovedCount);

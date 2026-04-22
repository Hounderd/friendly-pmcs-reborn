#if SPT_CLIENT
using EFT;
using FriendlyPMC.CoreFollowers.Modules;
using FriendlyPMC.CoreFollowers.Patches;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class BotDebugLogger
{
    private const int MaxComparisonBots = 2;
    private const float MaxComparisonDistanceToPlayer = 20f;
    private const float MaxComparisonDistanceToFollower = 18f;

    private readonly FollowerRegistry registry;
    private readonly Action<string> logInfo;
    private readonly Dictionary<string, BotDebugSnapshot> previousSnapshots = new(StringComparer.Ordinal);
    private BotDebugTimingState timingState = new(0f);

    public BotDebugLogger(FollowerRegistry registry, Action<string> logInfo)
    {
        this.registry = registry;
        this.logInfo = logInfo;
    }

    public void Tick()
    {
        if (!BotDebugLoggingPolicy.ShouldSample(UnityEngine.Time.time, timingState))
        {
            return;
        }

        timingState = new BotDebugTimingState(BotDebugLoggingPolicy.GetNextSampleTime(UnityEngine.Time.time));

        if (GamePlayerOwner.MyPlayer is not { } localPlayer || BotsControllerStatePatch.ActiveController is not { } botsController)
        {
            return;
        }

        var localPlayerPosition = BotDebugSnapshotMapper.GetWorldPoint(localPlayer);
        var followerSnapshots = CaptureFollowerSnapshots(localPlayerPosition);
        var comparisonSnapshots = followerSnapshots.Count == 0
            ? Array.Empty<BotDebugSnapshot>()
            : CaptureComparisonSnapshots(botsController, localPlayer, localPlayerPosition, followerSnapshots);
        var currentSnapshots = followerSnapshots.Concat(comparisonSnapshots).ToArray();

        EmitStateChanges(currentSnapshots);
        EmitSnapshots(followerSnapshots);

        previousSnapshots.Clear();
        foreach (var snapshot in currentSnapshots)
        {
            previousSnapshots[snapshot.ProfileId] = snapshot;
        }
    }

    private IReadOnlyList<BotDebugSnapshot> CaptureFollowerSnapshots(BotDebugWorldPoint localPlayerPosition)
    {
        return registry.RuntimeFollowers
            .Where(follower => follower.IsOperational)
            .Select(follower =>
            {
                registry.TryGetActiveOrder(follower.Aid, out var activeOrder);
                return follower.CaptureDebugSnapshot(activeOrder, localPlayerPosition);
            })
            .ToArray();
    }

    private IReadOnlyList<BotDebugSnapshot> CaptureComparisonSnapshots(
        BotsController botsController,
        Player localPlayer,
        BotDebugWorldPoint localPlayerPosition,
        IReadOnlyList<BotDebugSnapshot> followerSnapshots)
    {
        var followerPositions = registry.RuntimeFollowers
            .Where(follower => follower.IsOperational)
            .Select(follower => follower.CurrentPosition)
            .ToArray();
        var followerProfileIds = followerSnapshots
            .Select(snapshot => snapshot.ProfileId)
            .ToHashSet(StringComparer.Ordinal);

        var candidates = botsController.Bots?.BotOwners?
            .Where(bot => bot is not null && !bot.IsDead && bot.ProfileId != localPlayer.ProfileId && !followerProfileIds.Contains(bot.ProfileId))
            .Select(bot =>
            {
                var point = BotDebugSnapshotMapper.GetWorldPoint(bot);
                var nearestFollowerDistance = followerPositions.Length == 0
                    ? float.PositiveInfinity
                    : followerPositions.Min(position => position.DistanceTo(point));
                return (bot, candidate: new BotDebugSelectionCandidate(
                    bot.ProfileId,
                    point.DistanceTo(localPlayerPosition),
                    nearestFollowerDistance));
            })
            .ToArray()
            ?? [];

        var selectedProfileIds = BotDebugSelectionPolicy.SelectComparisonBots(
                candidates.Select(x => x.candidate),
                MaxComparisonBots,
                MaxComparisonDistanceToPlayer,
                MaxComparisonDistanceToFollower)
            .Select(candidate => candidate.ProfileId)
            .ToHashSet(StringComparer.Ordinal);

        return candidates
            .Where(candidate => selectedProfileIds.Contains(candidate.bot.ProfileId))
            .Select(candidate => BotDebugSnapshotMapper.CreateComparisonSnapshot(candidate.bot, localPlayerPosition, followerPositions))
            .ToArray();
    }

    private void EmitStateChanges(IEnumerable<BotDebugSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!previousSnapshots.TryGetValue(snapshot.ProfileId, out var previous))
            {
                logInfo($"BotState new: {BotDebugLogFormatter.FormatSnapshot(snapshot)}");
                continue;
            }

            var changes = BotDebugStateChangeDetector.DescribeChanges(previous, snapshot);
            if (changes.Count == 0)
            {
                continue;
            }

            logInfo($"BotState event: changes={string.Join("|", changes)}, {BotDebugLogFormatter.FormatSnapshot(snapshot)}");
        }
    }

    private void EmitSnapshots(IEnumerable<BotDebugSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots.OrderBy(snapshot => snapshot.DistanceToPlayer))
        {
            logInfo($"BotState snapshot: {BotDebugLogFormatter.FormatSnapshot(snapshot)}");
        }
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class BotDebugLogger
{
    public BotDebugLogger(Modules.FollowerRegistry registry, Action<string> logInfo)
    {
    }

    public void Tick()
    {
    }
}
#endif

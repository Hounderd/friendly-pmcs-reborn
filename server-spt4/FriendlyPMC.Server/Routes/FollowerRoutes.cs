using FriendlyPMC.Server.Models.Requests;
using FriendlyPMC.Server.Models.Responses;
using FriendlyPMC.Server.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace FriendlyPMC.Server.Routes;

[Injectable]
public sealed class FollowerRoutes(JsonUtil jsonUtil, FollowerRouteCallbacks callbacks)
    : StaticRouter(jsonUtil,
    [
        new RouteAction<EmptyRequestData>(
            "/pmcsquadmates/active",
            async (url, info, sessionId, output) => await callbacks.GetActiveFollowersAsync(url, info, sessionId)
        ),
        new RouteAction<EmptyRequestData>(
            "/pmcsquadmates/manager/roster",
            async (url, info, sessionId, output) => await callbacks.GetFollowerManagerRosterAsync(url, info, sessionId)
        ),
        new RouteAction<AddFollowerManagerMemberRequest>(
            "/pmcsquadmates/manager/member/add",
            async (url, info, sessionId, output) => await callbacks.AddFollowerManagerMemberAsync(url, info, sessionId)
        ),
        new RouteAction<RenameFollowerManagerMemberRequest>(
            "/pmcsquadmates/manager/member/rename",
            async (url, info, sessionId, output) => await callbacks.RenameFollowerManagerMemberAsync(url, info, sessionId)
        ),
        new RouteAction<DeleteFollowerManagerMemberRequest>(
            "/pmcsquadmates/manager/member/delete",
            async (url, info, sessionId, output) => await callbacks.DeleteFollowerManagerMemberAsync(url, info, sessionId)
        ),
        new RouteAction<SetFollowerManagerMemberAutoJoinRequest>(
            "/pmcsquadmates/manager/member/autojoin",
            async (url, info, sessionId, output) => await callbacks.SetFollowerManagerMemberAutoJoinAsync(url, info, sessionId)
        ),
        new RouteAction<SetFollowerManagerMemberLoadoutModeRequest>(
            "/pmcsquadmates/manager/member/loadout-mode",
            async (url, info, sessionId, output) => await callbacks.SetFollowerManagerMemberLoadoutModeAsync(url, info, sessionId)
        ),
        new RouteAction<GetFollowerInventoryRequest>(
            "/pmcsquadmates/inventory/get",
            async (url, info, sessionId, output) => await callbacks.GetFollowerInventoryAsync(url, info, sessionId)
        ),
        new RouteAction<FollowerInventoryMoveRequest>(
            "/pmcsquadmates/inventory/move",
            async (url, info, sessionId, output) => await callbacks.MoveFollowerInventoryAsync(url, info, sessionId)
        ),
        new RouteAction<RegisterFollowerRecruitRequest>(
            "/pmcsquadmates/recruit",
            async (url, info, sessionId, output) => await callbacks.RegisterRecruitAsync(url, info, sessionId)
        ),
        new RouteAction<SaveFollowerRaidProgressRequest>(
            "/pmcsquadmates/raid-progress",
            async (url, info, sessionId, output) => await callbacks.SaveRaidProgressAsync(url, info, sessionId)
        ),
        new RouteAction<GenerateFollowerBotsRequest>(
            "/client/game/bot/followergenerate",
            async (url, info, sessionId, output) => await callbacks.GenerateFollowerBotsAsync(url, info, sessionId)
        ),
        new RouteAction<EmptyRequestData>(
            "/pmcsquadmates/debug/followergenerate-payload/active",
            async (url, info, sessionId, output) => await callbacks.ProbeActiveFollowerGeneratePayloadAsync(url, info, sessionId)
        ),
        new RouteAction<ProbeFollowerGeneratePayloadRequest>(
            "/pmcsquadmates/debug/followergenerate-payload",
            async (url, info, sessionId, output) => await callbacks.ProbeFollowerGeneratePayloadAsync(url, info, sessionId)
        )
    ])
{
}

[Injectable]
public sealed class FollowerRouteCallbacks(
    JsonUtil jsonUtil,
    FollowerManagerService managerService,
    FollowerInventoryService followerInventoryService,
    FollowerBotGenerationService followerBotGenerationService,
    FollowerPayloadDumpService followerPayloadDumpService,
    FollowerRaidStateService raidStateService,
    HttpResponseUtil httpResponseUtil,
    ISptLogger<FollowerRouteCallbacks> logger)
{
    private const string DefaultProbeLocation = "factory4_day";

    public async ValueTask<string> GetActiveFollowersAsync(string url, EmptyRequestData request, MongoId sessionId)
    {
        var persistedFollowers = await managerService.LoadActiveFollowersForRaidAsync(sessionId.ToString());
        logger.Info($"Follower active raid load: session={sessionId}, count={persistedFollowers.Count}");
        var response = new GetActiveFollowersResponse(raidStateService.PrepareForRaid(persistedFollowers));
        return httpResponseUtil.GetBody(response);
    }

    public async ValueTask<string> GetFollowerManagerRosterAsync(string url, EmptyRequestData request, MongoId sessionId)
    {
        var roster = await managerService.GetRosterViewAsync(sessionId.ToString());
        return httpResponseUtil.GetBody(new GetFollowerManagerRosterResponse(roster));
    }

    public async ValueTask<string> AddFollowerManagerMemberAsync(string url, AddFollowerManagerMemberRequest request, MongoId sessionId)
    {
        await managerService.AddFollowerAsync(sessionId.ToString(), request.Nickname);
        return httpResponseUtil.EmptyResponse();
    }

    public async ValueTask<string> RenameFollowerManagerMemberAsync(string url, RenameFollowerManagerMemberRequest request, MongoId sessionId)
    {
        await managerService.RenameFollowerAsync(sessionId.ToString(), request.Aid, request.Nickname);
        return httpResponseUtil.EmptyResponse();
    }

    public async ValueTask<string> DeleteFollowerManagerMemberAsync(string url, DeleteFollowerManagerMemberRequest request, MongoId sessionId)
    {
        await managerService.DeleteFollowerAsync(sessionId.ToString(), request.Aid);
        return httpResponseUtil.EmptyResponse();
    }

    public async ValueTask<string> SetFollowerManagerMemberAutoJoinAsync(string url, SetFollowerManagerMemberAutoJoinRequest request, MongoId sessionId)
    {
        await managerService.SetAutoJoinAsync(sessionId.ToString(), request.Aid, request.AutoJoin);
        return httpResponseUtil.EmptyResponse();
    }

    public async ValueTask<string> SetFollowerManagerMemberLoadoutModeAsync(string url, SetFollowerManagerMemberLoadoutModeRequest request, MongoId sessionId)
    {
        await managerService.SetLoadoutModeAsync(sessionId.ToString(), request.Aid, request.LoadoutMode);
        return httpResponseUtil.EmptyResponse();
    }

    public async ValueTask<string> GetFollowerInventoryAsync(string url, GetFollowerInventoryRequest request, MongoId sessionId)
    {
        if (string.IsNullOrWhiteSpace(request.Aid))
        {
            logger.Error($"Follower inventory get failed: session={sessionId}, follower=<blank>, error=missing aid request value");
            return httpResponseUtil.GetBody(new GetFollowerInventoryResponse(
                string.Empty,
                string.Empty,
                null,
                null,
                "Failed to load follower inventory.",
                "Missing aid request value."));
        }

        try
        {
            var response = await followerInventoryService.GetInventoryAsync(sessionId.ToString(), request.Aid);
            return response is null
                ? httpResponseUtil.EmptyResponse()
                : httpResponseUtil.GetBody(response);
        }
        catch (Exception ex)
        {
            logger.Error($"Follower inventory get failed: session={sessionId}, follower={request.Aid}, error={ex}");
            return httpResponseUtil.GetBody(new GetFollowerInventoryResponse(
                request.Aid,
                string.Empty,
                null,
                null,
                "Failed to load follower inventory.",
                ex.GetType().Name + ": " + ex.Message));
        }
    }

    public async ValueTask<string> MoveFollowerInventoryAsync(string url, FollowerInventoryMoveRequest request, MongoId sessionId)
    {
        var response = await followerInventoryService.MoveAsync(sessionId.ToString(), request);
        return httpResponseUtil.GetBody(response);
    }

    public async ValueTask<string> RegisterRecruitAsync(string url, RegisterFollowerRecruitRequest request, MongoId sessionId)
    {
        await managerService.RegisterRecruitAsync(sessionId.ToString(), request.Follower);
        return httpResponseUtil.EmptyResponse();
    }

    public async ValueTask<string> SaveRaidProgressAsync(string url, SaveFollowerRaidProgressRequest request, MongoId sessionId)
    {
        logger.Info(
            $"Follower raid progress save: session={sessionId}, followers={request.Followers.Count}, raidStart={request.RaidStartFollowerAids?.Count ?? 0}, spawned={request.SpawnedFollowerAids?.Count ?? 0}, dead={request.DeadFollowerAids?.Count ?? 0}");
        await managerService.SaveRaidProgressAsync(
            sessionId.ToString(),
            request.Followers,
            request.RaidStartFollowerAids,
            request.SpawnedFollowerAids,
            request.DeadFollowerAids);
        return httpResponseUtil.EmptyResponse();
    }

    public async ValueTask<string> GenerateFollowerBotsAsync(string url, GenerateFollowerBotsRequest request, MongoId sessionId)
    {
        logger.Info(
            $"Follower generation request received: session={sessionId}, member={request.MemberId ?? "<null>"}, hasInfo={request.Info is not null}");

        try
        {
            var generatedBots = (await followerBotGenerationService.GenerateFollowerBotsAsync(sessionId, request)).ToArray();
            logger.Info(
                $"Follower generation produced bots: session={sessionId}, member={request.MemberId ?? "<null>"}, count={generatedBots.Length}");

            var normalizedPayload = FollowerHttpPayloadNormalizer.Normalize(generatedBots, request.MemberId);
            followerPayloadDumpService.CaptureFollowerGeneratePayload(sessionId.ToString(), request.MemberId, normalizedPayload);
            logger.Info(
                $"Follower generation response normalized: session={sessionId}, member={request.MemberId ?? "<null>"}, payloadType={normalizedPayload?.GetType().Name ?? "<null>"}");

            return httpResponseUtil.GetBody(normalizedPayload);
        }
        catch (Exception ex)
        {
            logger.Error(
                $"Follower generation failed: session={sessionId}, member={request.MemberId ?? "<null>"}, hasInfo={request.Info is not null}, error={ex}");
            throw;
        }
    }

    public async ValueTask<string> ProbeFollowerGeneratePayloadAsync(string url, ProbeFollowerGeneratePayloadRequest request, MongoId sessionId)
    {
        var requestedSessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? sessionId
            : new MongoId(request.SessionId);
        return await BuildFollowerGeneratePayloadProbeResponseAsync(
            requestedSessionId,
            request.MemberId,
            request.Role,
            request.Difficulty,
            request.Limit,
            request.Location);
    }

    public async ValueTask<string> ProbeActiveFollowerGeneratePayloadAsync(string url, EmptyRequestData request, MongoId sessionId)
    {
        var activeFollowers = await managerService.LoadActiveFollowersForRaidAsync(sessionId.ToString());
        var activeFollower = activeFollowers.FirstOrDefault();
        var activeMemberId = activeFollower?.Aid ?? string.Empty;
        return await BuildFollowerGeneratePayloadProbeResponseAsync(
            sessionId,
            activeMemberId,
            null,
            "hard",
            1,
            DefaultProbeLocation);
    }

    private async ValueTask<string> BuildFollowerGeneratePayloadProbeResponseAsync(
        MongoId sessionId,
        string? memberId,
        string? role,
        string? difficulty,
        int? limit,
        string? location)
    {
        var requestedMemberId = memberId;
        if (string.IsNullOrWhiteSpace(requestedMemberId))
        {
            return httpResponseUtil.GetBody(FollowerPayloadProbeBuilder.BuildError(
                sessionId.ToString(),
                string.Empty,
                "No active persisted follower was available to probe."));
        }

        var persistedFollower = await managerService.TryGetFollowerForRaidAsync(sessionId.ToString(), requestedMemberId);
        if (persistedFollower is null)
        {
            return httpResponseUtil.GetBody(FollowerPayloadProbeBuilder.BuildError(
                sessionId.ToString(),
                requestedMemberId,
                $"No persisted follower profile was found for aid '{requestedMemberId}'."));
        }

        var resolvedRole = !string.IsNullOrWhiteSpace(role)
            ? NormalizeProbeRole(role)
            : InferProbeRole(persistedFollower?.Side);
        var resolvedDifficulty = string.IsNullOrWhiteSpace(difficulty) ? "hard" : difficulty;
        var resolvedLimit = limit is > 0 ? limit.Value : 1;
        var resolvedLocation = string.IsNullOrWhiteSpace(location) ? DefaultProbeLocation : location;

        try
        {
            var generatedBots = await followerBotGenerationService.GenerateFollowerBotProbeAsync(
                sessionId,
                requestedMemberId,
                resolvedRole,
                resolvedDifficulty,
                resolvedLimit,
                resolvedLocation);

            var normalizedPayload = FollowerHttpPayloadNormalizer.Normalize(generatedBots, requestedMemberId);
            var response = FollowerPayloadProbeBuilder.Build(
                sessionId.ToString(),
                requestedMemberId,
                normalizedPayload,
                jsonUtil);
            logger.Info($"Follower payload probe generated: session={sessionId}, member={requestedMemberId}, location={resolvedLocation}, count={response.BotCount}, missingRootKeys={response.MissingClientRootKeys.Count}, nullPaths={response.NullPaths.Count}");
            return httpResponseUtil.GetBody(response);
        }
        catch (Exception ex)
        {
            logger.Error($"Follower payload probe failed: session={sessionId}, member={requestedMemberId}, location={resolvedLocation}, error={ex}");
            return httpResponseUtil.GetBody(FollowerPayloadProbeBuilder.BuildError(
                sessionId.ToString(),
                requestedMemberId,
                ex.ToString()));
        }
    }

    private static string InferProbeRole(string? side)
    {
        return string.Equals(side, "Bear", StringComparison.OrdinalIgnoreCase)
            ? "pmcBEAR"
            : "pmcUSEC";
    }

    private static string NormalizeProbeRole(string role)
    {
        if (string.Equals(role, "bear", StringComparison.OrdinalIgnoreCase))
        {
            return "pmcBEAR";
        }

        if (string.Equals(role, "usec", StringComparison.OrdinalIgnoreCase))
        {
            return "pmcUSEC";
        }

        return role;
    }
}

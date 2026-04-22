using FriendlyPMC.CoreFollowers.Models;
using Newtonsoft.Json;

#if SPT_CLIENT
using SPT.Common.Http;
#endif

namespace FriendlyPMC.CoreFollowers.Services;

public interface IFollowerApiClient
{
    Task<IReadOnlyList<FollowerSnapshotDto>> GetActiveFollowersAsync();

    Task RegisterRecruitAsync(FollowerSnapshotDto follower);

    Task SaveRaidProgressAsync(FollowerRaidProgressPayload payload);

    Task<FollowerInventoryViewDto?> GetFollowerInventoryAsync(string followerAid);

    Task<FollowerInventoryMoveResultDto> MoveFollowerInventoryItemAsync(FollowerInventoryMovePayload payload);
}

public interface IRequestDispatcher
{
    string GetJson(string route);

    string PostJson(string route, string body);
}

public sealed class FollowerApiClient : IFollowerApiClient
{
    private const string ActiveFollowersRoute = "/friendlypmc/corefollowers/active";
    private const string RegisterRecruitRoute = "/friendlypmc/corefollowers/recruit";
    private const string SaveRaidProgressRoute = "/friendlypmc/corefollowers/raid-progress";
    private const string GetFollowerInventoryRoute = "/friendlypmc/corefollowers/inventory/get";
    private const string MoveFollowerInventoryRoute = "/friendlypmc/corefollowers/inventory/move";

    private readonly IRequestDispatcher requestDispatcher;

    public FollowerApiClient()
        : this(CreateDefaultDispatcher())
    {
    }

    public FollowerApiClient(IRequestDispatcher requestDispatcher)
    {
        this.requestDispatcher = requestDispatcher;
    }

    public Task<IReadOnlyList<FollowerSnapshotDto>> GetActiveFollowersAsync()
    {
        var json = requestDispatcher.GetJson(ActiveFollowersRoute);
        var response = JsonConvert.DeserializeObject<GetActiveFollowersResponseDto>(json);
        if (response?.Followers is null)
        {
            response = JsonConvert.DeserializeObject<WrappedResponseDto<GetActiveFollowersResponseDto>>(json)?.Data;
        }

        response ??= new GetActiveFollowersResponseDto(Array.Empty<FollowerProfileSnapshotDto>());
        var followers = response.Followers ?? Array.Empty<FollowerProfileSnapshotDto>();

        IReadOnlyList<FollowerSnapshotDto> mappedFollowers = followers
            .Select(follower =>
            {
                var healthParts = follower.Health?.Parts ?? new Dictionary<string, HealthPartDto>(StringComparer.Ordinal);
                return new FollowerSnapshotDto(
                    follower.Aid,
                    follower.Nickname,
                    follower.Side,
                    follower.Level,
                    follower.Experience,
                    follower.SkillProgress,
                    follower.InventoryItemIds,
                    healthParts.ToDictionary(
                    part => part.Key,
                    part => part.Value.Current,
                    StringComparer.Ordinal),
                    healthParts.ToDictionary(
                        part => part.Key,
                        part => part.Value.Maximum,
                        StringComparer.Ordinal),
                    follower.Equipment is null
                        ? null
                        : new FollowerEquipmentSnapshotDto(
                            follower.Equipment.EquipmentId,
                            follower.Equipment.Items
                                .Select(item => new FollowerEquipmentItemSnapshotDto(
                                    item.Id,
                                    item.TemplateId,
                                    item.ParentId,
                                    item.SlotId,
                                    item.LocationJson,
                                    item.UpdJson))
                                .ToArray()),
                    follower.Appearance is null
                        ? null
                        : new FollowerAppearanceSnapshotDto(
                            follower.Appearance.Head,
                            follower.Appearance.Body,
                            follower.Appearance.Feet,
                            follower.Appearance.Hands,
                            follower.Appearance.Voice,
                            follower.Appearance.DogTag));
            })
            .ToArray();

        return Task.FromResult(mappedFollowers);
    }

    public Task RegisterRecruitAsync(FollowerSnapshotDto follower)
    {
        var body = JsonConvert.SerializeObject(
            new RegisterFollowerRecruitRequestDto(
                new FollowerRosterRecordDto(follower.Aid, follower.Nickname, follower.Side)));
        requestDispatcher.PostJson(RegisterRecruitRoute, body);
        return Task.CompletedTask;
    }

    public Task SaveRaidProgressAsync(FollowerRaidProgressPayload payload)
    {
        var body = JsonConvert.SerializeObject(
            new SaveFollowerRaidProgressRequestDto(
                payload.Followers.Select(
                    follower => new FollowerProfileSnapshotDto(
                        follower.Aid,
                        follower.Nickname,
                        follower.Side,
                        follower.Level,
                        follower.Experience,
                        follower.SkillProgress,
                        follower.InventoryItemIds,
                        BuildHealthDto(follower),
                        BuildEquipmentDto(follower),
                        BuildAppearanceDto(follower)))
                    .ToArray(),
                payload.RaidStartFollowerAids,
                payload.SpawnedFollowerAids,
                payload.DeadFollowerAids));
        requestDispatcher.PostJson(SaveRaidProgressRoute, body);
        return Task.CompletedTask;
    }

    public Task<FollowerInventoryViewDto?> GetFollowerInventoryAsync(string followerAid)
    {
        var body = JsonConvert.SerializeObject(new GetFollowerInventoryRequestDto(followerAid));
        string json;
        try
        {
            json = requestDispatcher.PostJson(GetFollowerInventoryRoute, body);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Follower inventory request failed for aid={followerAid} route={GetFollowerInventoryRoute}: {ex.Message}",
                ex);
        }

        var response = JsonConvert.DeserializeObject<GetFollowerInventoryResponseDto>(json);
        if (response?.Player is null && response?.Follower is null && string.IsNullOrWhiteSpace(response?.ErrorMessage))
        {
            response = JsonConvert.DeserializeObject<WrappedResponseDto<GetFollowerInventoryResponseDto>>(json)?.Data;
        }

        if (response is null)
        {
            return Task.FromResult<FollowerInventoryViewDto?>(null);
        }

        return Task.FromResult<FollowerInventoryViewDto?>(
            new FollowerInventoryViewDto(
                response.FollowerAid,
                response.Nickname,
                response.Player is null ? null : MapInventoryOwner(response.Player),
                response.Follower is null ? null : MapInventoryOwner(response.Follower),
                response.ErrorMessage,
                response.DebugDetails));
    }

    public Task<FollowerInventoryMoveResultDto> MoveFollowerInventoryItemAsync(FollowerInventoryMovePayload payload)
    {
        var body = JsonConvert.SerializeObject(
            new FollowerInventoryMoveRequestDto(
                payload.FollowerAid,
                payload.SourceOwner,
                payload.ItemId,
                payload.ToId,
                payload.ToContainer,
                payload.ToLocationJson));
        try
        {
            var json = requestDispatcher.PostJson(MoveFollowerInventoryRoute, body);
            var response = JsonConvert.DeserializeObject<WrappedResponseDto<FollowerInventoryMoveResponseDto>>(json)?.Data
                ?? JsonConvert.DeserializeObject<FollowerInventoryMoveResponseDto>(json);

            response ??= new FollowerInventoryMoveResponseDto(false, "Inventory move failed.", null);
            return Task.FromResult(new FollowerInventoryMoveResultDto(response.Succeeded, response.ErrorMessage));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Follower inventory move request failed for aid={payload.FollowerAid} item={payload.ItemId} route={MoveFollowerInventoryRoute}: {ex.Message}",
                ex);
        }
    }

    private static FollowerEquipmentDto? BuildEquipmentDto(FollowerSnapshotDto follower)
    {
        if (follower.Equipment is null)
        {
            return null;
        }

        return new FollowerEquipmentDto(
            follower.Equipment.EquipmentId,
            follower.Equipment.Items
                .Select(item => new FollowerEquipmentItemDto(
                    item.Id,
                    item.TemplateId,
                    item.ParentId,
                    item.SlotId,
                    item.LocationJson,
                    item.UpdJson))
                .ToArray());
    }

    private static FollowerAppearanceDto? BuildAppearanceDto(FollowerSnapshotDto follower)
    {
        if (follower.Appearance is null)
        {
            return null;
        }

        return new FollowerAppearanceDto(
            follower.Appearance.Head,
            follower.Appearance.Body,
            follower.Appearance.Feet,
            follower.Appearance.Hands,
            follower.Appearance.Voice,
            follower.Appearance.DogTag);
    }

    private static FollowerHealthDto BuildHealthDto(FollowerSnapshotDto follower)
    {
        var parts = follower.HealthValues
            .Select(part => new KeyValuePair<string, HealthPartDto>(
                part.Key,
                new HealthPartDto(
                    part.Value,
                    GetHealthMaximumValue(follower, part.Key, part.Value))))
            .ToDictionary(part => part.Key, part => part.Value, StringComparer.Ordinal);

        return new FollowerHealthDto(parts);
    }

    private static int GetHealthMaximumValue(FollowerSnapshotDto follower, string bodyPart, int fallbackCurrentValue)
    {
        return follower.HealthMaximumValues.TryGetValue(bodyPart, out var value)
            ? value
            : fallbackCurrentValue;
    }

    private static IRequestDispatcher CreateDefaultDispatcher()
    {
#if SPT_CLIENT
        return new RequestHandlerDispatcher();
#else
        return new NoopRequestDispatcher();
#endif
    }

    private static FollowerInventoryOwnerViewDto MapInventoryOwner(FollowerInventoryOwnerDto owner)
    {
        return new FollowerInventoryOwnerViewDto(
            owner.Owner,
            owner.RootId,
            (owner.Items ?? Array.Empty<FollowerInventoryItemDto>())
                .Select(item => new FollowerInventoryItemViewDto(
                    item.Id,
                    item.TemplateId,
                    item.ParentId,
                    item.SlotId,
                    item.LocationJson,
                    item.UpdJson))
                .ToArray());
    }
}

internal sealed record GetActiveFollowersResponseDto(IReadOnlyList<FollowerProfileSnapshotDto> Followers);
internal sealed record WrappedResponseDto<T>(T? Data);

internal sealed record FollowerProfileSnapshotDto(
    string Aid,
    string Nickname,
    string Side,
    int Level,
    int Experience,
    IReadOnlyDictionary<string, int> SkillProgress,
    IReadOnlyList<string> InventoryItemIds,
    FollowerHealthDto? Health,
    FollowerEquipmentDto? Equipment,
    FollowerAppearanceDto? Appearance);

internal sealed record FollowerHealthDto(IReadOnlyDictionary<string, HealthPartDto>? Parts);

internal sealed record HealthPartDto(int Current, int Maximum);

internal sealed record FollowerEquipmentDto(string EquipmentId, IReadOnlyList<FollowerEquipmentItemDto> Items);

internal sealed record FollowerAppearanceDto(
    string? Head,
    string? Body,
    string? Feet,
    string? Hands,
    string? Voice,
    string? DogTag);

internal sealed record FollowerEquipmentItemDto(
    string Id,
    string TemplateId,
    string? ParentId,
    string? SlotId,
    string? LocationJson,
    string? UpdJson);

internal sealed record FollowerRosterRecordDto(string Aid, string Nickname, string Side);

internal sealed record RegisterFollowerRecruitRequestDto(FollowerRosterRecordDto Follower);

internal sealed record SaveFollowerRaidProgressRequestDto(
    IReadOnlyList<FollowerProfileSnapshotDto> Followers,
    IReadOnlyList<string> RaidStartFollowerAids,
    IReadOnlyList<string> SpawnedFollowerAids,
    IReadOnlyList<string> DeadFollowerAids);

internal sealed record GetFollowerInventoryResponseDto(
    string FollowerAid,
    string Nickname,
    FollowerInventoryOwnerDto? Player,
    FollowerInventoryOwnerDto? Follower,
    string? ErrorMessage = null,
    string? DebugDetails = null);

internal sealed record FollowerInventoryOwnerDto(
    string Owner,
    string RootId,
    IReadOnlyList<FollowerInventoryItemDto>? Items);

internal sealed record FollowerInventoryItemDto(
    string Id,
    string TemplateId,
    string? ParentId,
    string? SlotId,
    string? LocationJson,
    string? UpdJson);

internal sealed record FollowerInventoryMoveRequestDto(
    string FollowerAid,
    string SourceOwner,
    string ItemId,
    string ToId,
    string ToContainer,
    string? ToLocationJson);

internal sealed record GetFollowerInventoryRequestDto(string Aid);

internal sealed record FollowerInventoryMoveResponseDto(
    bool Succeeded,
    string? ErrorMessage,
    object? Inventory);

internal sealed class NoopRequestDispatcher : IRequestDispatcher
{
    public string GetJson(string route)
    {
        return "{}";
    }

    public string PostJson(string route, string body)
    {
        return "{}";
    }
}

#if SPT_CLIENT
internal sealed class RequestHandlerDispatcher : IRequestDispatcher
{
    public string GetJson(string route)
    {
        return RequestHandler.GetJson(route);
    }

    public string PostJson(string route, string body)
    {
        return RequestHandler.PostJson(route, body);
    }
}
#endif

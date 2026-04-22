using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record ProbeFollowerGeneratePayloadRequest(
    string SessionId,
    string MemberId,
    string? Role,
    string? Difficulty,
    int? Limit,
    string? Location) : IRequestData;

namespace FriendlyPMC.Server.Models.Responses;

public sealed record ProbeFollowerGeneratePayloadResponse(
    string SessionId,
    string MemberId,
    int BotCount,
    string SerializedJson,
    IReadOnlyList<string> FirstBotRootKeys,
    IReadOnlyList<string> MissingClientRootKeys,
    IReadOnlyList<string> NullPaths,
    string? Error);

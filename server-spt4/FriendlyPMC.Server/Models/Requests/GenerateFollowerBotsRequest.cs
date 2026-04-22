using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record GenerateFollowerBotsRequest(GenerateBotsRequestData? Info, string? MemberId) : IRequestData;

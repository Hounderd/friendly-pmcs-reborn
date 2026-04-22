using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record AddFollowerManagerMemberRequest(string? Nickname) : IRequestData;

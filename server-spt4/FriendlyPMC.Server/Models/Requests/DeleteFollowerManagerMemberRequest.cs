using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record DeleteFollowerManagerMemberRequest(string Aid) : IRequestData;

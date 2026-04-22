using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record RenameFollowerManagerMemberRequest(string Aid, string Nickname) : IRequestData;

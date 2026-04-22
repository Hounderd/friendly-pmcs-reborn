using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record SetFollowerManagerMemberAutoJoinRequest(string Aid, bool AutoJoin) : IRequestData;

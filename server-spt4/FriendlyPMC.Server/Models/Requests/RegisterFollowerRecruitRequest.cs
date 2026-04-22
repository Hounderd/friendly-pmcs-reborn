using FriendlyPMC.Server.Models;
using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record RegisterFollowerRecruitRequest(FollowerRosterRecord Follower) : IRequestData;

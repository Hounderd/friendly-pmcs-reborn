using SPTarkov.Server.Core.Models.Utils;

namespace FriendlyPMC.Server.Models.Requests;

public sealed record SetFollowerManagerMemberLoadoutModeRequest(string Aid, string LoadoutMode) : IRequestData;

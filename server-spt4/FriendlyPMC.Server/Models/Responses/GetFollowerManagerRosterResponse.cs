namespace FriendlyPMC.Server.Models.Responses;

public sealed record FollowerManagerMemberDto(
    string Aid,
    string Nickname,
    string Side,
    bool AutoJoin,
    string LoadoutMode,
    string? AssignedEquipmentBuildName,
    int Level,
    int Experience,
    bool HasStoredProfile);

public sealed record GetFollowerManagerRosterResponse(IReadOnlyList<FollowerManagerMemberDto> Members);

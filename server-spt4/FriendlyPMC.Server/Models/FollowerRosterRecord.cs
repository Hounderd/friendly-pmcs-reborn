namespace FriendlyPMC.Server.Models;

public sealed record FollowerRosterRecord(
    string Aid,
    string Nickname,
    string Side,
    bool AutoJoin = true,
    string LoadoutMode = FollowerLoadoutModes.Persisted,
    string? AssignedEquipmentBuildName = null
);

public static class FollowerLoadoutModes
{
    public const string Persisted = "Persisted";
    public const string Generated = "Generated";
}

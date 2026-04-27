namespace FriendlyPMC.Server.Configuration;

public sealed class CoreFollowerConfig
{
    public string DataDirectory { get; init; } = "user/mods/PMCSquadmates.Server/data";
    public bool EnableRecruitment { get; init; } = true;
}

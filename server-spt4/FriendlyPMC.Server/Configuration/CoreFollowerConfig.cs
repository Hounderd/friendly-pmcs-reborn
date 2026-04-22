namespace FriendlyPMC.Server.Configuration;

public sealed class CoreFollowerConfig
{
    public string DataDirectory { get; init; } = "user/mods/FriendlyPMC.CoreFollowers/data";
    public bool EnableRecruitment { get; init; } = true;
}

using SPTarkov.Server.Core.Models.Spt.Mod;

namespace FriendlyPMC.Server;

public sealed record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "xyz.pit.friendlypmc.corefollowers";
    public override string Name { get; init; } = "FriendlyPMC.CoreFollowers";
    public override string Author { get; init; } = "Hounderd";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new(FriendlyPmcBuildVersion.Value);
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://github.com/Hounderd/friendly-pmcs-reborn";
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

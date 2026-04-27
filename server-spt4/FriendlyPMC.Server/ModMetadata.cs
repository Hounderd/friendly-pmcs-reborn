using SPTarkov.Server.Core.Models.Spt.Mod;

namespace FriendlyPMC.Server;

public sealed record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.hounderd.pmcsquadmates.server";
    public override string Name { get; init; } = "PMC Squadmates";
    public override string Author { get; init; } = "Hounderd";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new(FriendlyPmcBuildVersion.Value);
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://github.com/Hounderd/pmc-squadmates";
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

namespace FriendlyPMC.CoreFollowers.Services;

public static class FriendlyFollowerBrainRegistrationProfile
{
    private static readonly string[] SupportedBrainNames =
    {
        "PmcBear",
        "PmcUsec",
        "Assault",
        "CursAssault",
        "Marksman",
        "ExUsec",
        "PMC",
    };

    public static IReadOnlyList<string> GetSupportedBrainNames()
    {
        return SupportedBrainNames;
    }
}

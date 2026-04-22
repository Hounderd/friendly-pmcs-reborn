#if SPT_CLIENT
using DrakiaXYZ.BigBrain.Brains;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FriendlyFollowerBigBrainBootstrap
{
    private const int MovementLayerPriority = 90;
    private static bool registered;

    public static void RegisterLayers(Action<string> logInfo)
    {
        if (registered)
        {
            return;
        }

        var supportedBrains = FriendlyFollowerBrainRegistrationProfile.GetSupportedBrainNames().ToList();
        BrainManager.AddCustomLayer(
            typeof(FriendlyFollowerMovementLayer),
            supportedBrains,
            MovementLayerPriority);

        registered = true;
        logInfo($"Registered FriendlyPMC BigBrain movement layer for: {string.Join(", ", supportedBrains)}");
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class FriendlyFollowerBigBrainBootstrap
{
    public static void RegisterLayers(Action<string> logInfo)
    {
    }
}
#endif

#if SPT_CLIENT
using EFT;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerLootingRuntimeDisabler
{
    private const float DefaultSuppressionDurationSeconds = 10f;

    public static FollowerLootingRuntimeDisableResult DisableForFollower(BotOwner owner, Action<string> logInfo)
    {
        if (!LootingBotsInteropBridge.IsLootingBotsLoaded())
        {
            return new FollowerLootingRuntimeDisableResult(
                CompatibilitySatisfied: true,
                AppliedSuppression: false);
        }

        var appliedSuppression = LootingBotsInteropBridge.TryPreventBotFromLooting(owner, DefaultSuppressionDurationSeconds);
        return new FollowerLootingRuntimeDisableResult(
            CompatibilitySatisfied: appliedSuppression,
            AppliedSuppression: appliedSuppression);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerLootingRuntimeDisabler
{
    public static FollowerLootingRuntimeDisableResult DisableForFollower(object owner, Action<string> logInfo)
    {
        return new FollowerLootingRuntimeDisableResult(
            CompatibilitySatisfied: true,
            AppliedSuppression: false);
    }
}
#endif

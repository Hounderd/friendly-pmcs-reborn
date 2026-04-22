using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerLootingCompatibilityPolicy
{
    private static readonly HashSet<string> DisabledLootingComponentTypeNames = new(StringComparer.Ordinal)
    {
        "LootingBots.Components.LootingBrain",
        "LootingBots.Components.LootFinder",
    };

    public static bool ShouldSuppressLooting(FollowerCommand? activeOrder)
    {
        return activeOrder.HasValue
            && activeOrder.Value != FollowerCommand.Loot;
    }

    public static bool ShouldDisableLootingComponent(string? typeName)
    {
        return !string.IsNullOrWhiteSpace(typeName)
            && DisabledLootingComponentTypeNames.Contains(typeName);
    }
}

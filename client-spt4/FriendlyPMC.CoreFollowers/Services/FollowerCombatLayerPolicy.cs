namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerCombatLayerPolicy
{
    public static bool IsCombatLayer(string? activeLayerName)
    {
        if (string.IsNullOrWhiteSpace(activeLayerName))
        {
            return false;
        }

        var normalized = activeLayerName.Trim();
        return string.Equals(normalized, "CombatSoloLayer", StringComparison.Ordinal)
            || string.Equals(normalized, "SAINAvoidThreatLayer", StringComparison.Ordinal)
            || normalized.Contains("Combat", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AvoidThreat", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLootingLayer(string? activeLayerName)
    {
        if (string.IsNullOrWhiteSpace(activeLayerName))
        {
            return false;
        }

        return activeLayerName.Contains("Loot", StringComparison.OrdinalIgnoreCase);
    }
}

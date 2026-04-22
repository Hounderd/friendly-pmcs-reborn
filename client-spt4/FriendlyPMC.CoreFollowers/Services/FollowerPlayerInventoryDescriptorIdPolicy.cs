namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerPlayerInventoryDescriptorIdPolicy
{
    public static string? NormalizeRootId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 24 ? trimmed : null;
    }
}

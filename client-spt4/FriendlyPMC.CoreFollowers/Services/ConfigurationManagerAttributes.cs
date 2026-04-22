namespace FriendlyPMC.CoreFollowers.Services;

#if SPT_CLIENT
#pragma warning disable CS0649
internal sealed class ConfigurationManagerAttributes
{
    public bool? Browsable;
    public string? Category;
    public object? DefaultValue;
    public bool? HideDefaultButton;
    public bool? HideSettingName;
    public string? Description;
    public string? DispName;
    public int? Order;
    public bool? ReadOnly;
    public bool? IsAdvanced;
}
#pragma warning restore CS0649
#endif

namespace FriendlyPMC.Server.Services;

internal static class FollowerServerHarmonyBridge
{
    private static FollowerManagerSocialViewService? socialViewService;
    private static PlayerProfileIntegrityService? playerProfileIntegrityService;
    private static Action<string>? errorLogger;

    public static void Initialize(
        FollowerManagerSocialViewService service,
        PlayerProfileIntegrityService profileIntegrityService,
        Action<string> logError)
    {
        socialViewService = service;
        playerProfileIntegrityService = profileIntegrityService;
        errorLogger = logError;
    }

    public static FollowerManagerSocialViewService? SocialViewService => socialViewService;
    public static PlayerProfileIntegrityService? PlayerProfileIntegrityService => playerProfileIntegrityService;

    public static void LogError(string message, Exception ex)
    {
        errorLogger?.Invoke($"{message}: {ex}");
    }
}

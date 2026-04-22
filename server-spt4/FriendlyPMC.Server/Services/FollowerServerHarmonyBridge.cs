namespace FriendlyPMC.Server.Services;

internal static class FollowerServerHarmonyBridge
{
    private static FollowerManagerSocialViewService? socialViewService;
    private static Action<string>? errorLogger;

    public static void Initialize(FollowerManagerSocialViewService service, Action<string> logError)
    {
        socialViewService = service;
        errorLogger = logError;
    }

    public static FollowerManagerSocialViewService? SocialViewService => socialViewService;

    public static void LogError(string message, Exception ex)
    {
        errorLogger?.Invoke($"{message}: {ex}");
    }
}

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct BotDebugTimingState(float NextSampleTime);

public static class BotDebugLoggingPolicy
{
    public const float SampleIntervalSeconds = 1.0f;

    public static bool ShouldSample(float now, BotDebugTimingState state, bool diagnosticsEnabled = true)
    {
        return diagnosticsEnabled && now >= state.NextSampleTime;
    }

    public static float GetNextSampleTime(float now)
    {
        return now + SampleIntervalSeconds;
    }
}

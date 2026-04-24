#if SPT_CLIENT
using System.Reflection;
using EFT;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class SainFollowerAccuracyRuntimeTuner
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool TryApply(BotOwner botOwner, string followerName, Action<string>? logInfo)
    {
        var current = botOwner.Settings?.Current;
        if (current is null)
        {
            return false;
        }

        var changed = false;
        changed |= TryUpdateFloatField(current, "_scatteringCoef", SainFollowerAccuracyPolicy.ResolveScatteringCoef);
        changed |= TryUpdateFloatField(current, "_accuratySpeedCoef", SainFollowerAccuracyPolicy.ResolveAccuracySpeedCoef);
        changed |= TryUpdateFloatField(current, "_precicingSpeedCoef", SainFollowerAccuracyPolicy.ResolvePrecisionSpeedCoef);
        changed |= TryUpdateFloatField(current, "_visibleDistCoef", SainFollowerAccuracyPolicy.ResolveVisibleDistanceCoef);
        changed |= TryUpdateFloatField(current, "_hearingDistCoef", SainFollowerAccuracyPolicy.ResolveHearingDistanceCoef);

        if (changed)
        {
            logInfo?.Invoke($"Follower SAIN accuracy tuning applied: follower={followerName}, profileId={botOwner.ProfileId}");
        }

        return true;
    }

    private static bool TryUpdateFloatField(object target, string fieldName, Func<float, float> resolve)
    {
        var field = target.GetType().GetField(fieldName, FieldFlags);
        if (field is null || field.FieldType != typeof(float))
        {
            return false;
        }

        var currentValue = field.GetValue(target);
        if (currentValue is not float current)
        {
            return false;
        }

        var next = resolve(current);
        if (MathF.Abs(next - current) < 0.001f)
        {
            return false;
        }

        field.SetValue(target, next);
        return true;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class SainFollowerAccuracyRuntimeTuner
{
    public static bool TryApply(object botOwner, string followerName, Action<string>? logInfo)
    {
        return false;
    }
}
#endif

#if SPT_CLIENT
using System.Reflection;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class SainFollowerAccuracyRuntimeTuner
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type? SainBotComponentType = AccessTools.TypeByName("SAIN.Components.BotComponent");

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
        changed |= TryUpdateSainProfileDifficulty(botOwner);

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

    private static bool TryUpdateSainProfileDifficulty(BotOwner botOwner)
    {
        if (SainBotComponentType is null || botOwner is not Component component)
        {
            return false;
        }

        var sainComponent = component.GetComponent(SainBotComponentType);
        var info = TryReadMember(sainComponent, "Info");
        var profile = TryReadMember(info, "Profile");
        if (profile is null)
        {
            return false;
        }

        var currentDifficulty = TryReadFloatMember(profile, "DifficultyModifier");
        if (!currentDifficulty.HasValue)
        {
            return false;
        }

        var nextDifficulty = SainFollowerAccuracyPolicy.ResolveSainDifficultyModifier(currentDifficulty.Value);
        var changed = TrySetFloatMember(profile, "DifficultyModifier", nextDifficulty);
        changed |= TrySetFloatMember(profile, "DifficultyModifierSqrt", MathF.Sqrt(nextDifficulty));

        var weaponInfo = TryReadMember(info, "WeaponInfo");
        changed |= TrySetBoolField(weaponInfo, "_forceNewCheck", true);
        return changed;
    }

    private static object? TryReadMember(object? target, string memberName)
    {
        if (target is null)
        {
            return null;
        }

        return target.GetType().GetProperty(memberName, FieldFlags)?.GetValue(target)
            ?? target.GetType().GetField(memberName, FieldFlags)?.GetValue(target);
    }

    private static float? TryReadFloatMember(object target, string memberName)
    {
        var value = TryReadMember(target, memberName);
        return value is float floatValue ? floatValue : null;
    }

    private static bool TrySetFloatMember(object target, string memberName, float value)
    {
        var property = target.GetType().GetProperty(memberName, FieldFlags);
        if (property?.PropertyType == typeof(float) && property.CanWrite)
        {
            var currentPropertyValue = property.GetValue(target) as float? ?? float.NaN;
            if (MathF.Abs(currentPropertyValue - value) < 0.001f)
            {
                return false;
            }

            property.SetValue(target, value);
            return true;
        }

        var field = target.GetType().GetField($"<{memberName}>k__BackingField", FieldFlags)
            ?? target.GetType().GetField(memberName, FieldFlags);
        if (field?.FieldType != typeof(float))
        {
            return false;
        }

        var currentValue = field.GetValue(target);
        if (currentValue is float current && MathF.Abs(current - value) < 0.001f)
        {
            return false;
        }

        field.SetValue(target, value);
        return true;
    }

    private static bool TrySetBoolField(object? target, string fieldName, bool value)
    {
        if (target is null)
        {
            return false;
        }

        var field = target.GetType().GetField(fieldName, FieldFlags);
        if (field?.FieldType != typeof(bool))
        {
            return false;
        }

        if (field.GetValue(target) is bool current && current == value)
        {
            return false;
        }

        field.SetValue(target, value);
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

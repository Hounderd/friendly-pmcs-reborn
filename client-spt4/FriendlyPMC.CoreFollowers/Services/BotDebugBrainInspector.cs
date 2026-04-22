#if SPT_CLIENT
using System.Collections.Concurrent;
using System.Reflection;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using HarmonyLib;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class BotDebugBrainInspector
{
    private static readonly FieldInfo? CustomLayerField = ResolveField("DrakiaXYZ.BigBrain.Internal.CustomLayerWrapper", "customLayer");
    private static readonly FieldInfo? CustomLogicField = ResolveField("DrakiaXYZ.BigBrain.Internal.CustomLogicWrapper", "customLogic");
    private static readonly ConcurrentDictionary<string, PropertyInfo?> PropertyCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, MethodInfo?> MethodCache = new(StringComparer.Ordinal);

    public static string GetActiveLayerName(BotOwner botOwner)
    {
        var activeLayer = BrainManager.GetActiveLayer(botOwner);
        var unwrapped = Unwrap(activeLayer, CustomLayerField);
        return ResolveDisplayName(
                unwrapped,
                activeLayer,
                botOwner.Brain,
                "ActiveLayerName")
            ?? "None";
    }

    public static string GetActiveLogicName(BotOwner botOwner)
    {
        var activeLogic = BrainManager.GetActiveLogic(botOwner);
        var unwrapped = Unwrap(activeLogic, CustomLogicField);
        return ResolveDisplayName(
                unwrapped,
                activeLogic,
                botOwner.Brain,
                "GetActiveNodeReason")
            ?? "None";
    }

    private static object? Unwrap(object? wrapped, FieldInfo? field)
    {
        if (wrapped is null || field is null)
        {
            return null;
        }

        return field.DeclaringType?.IsInstanceOfType(wrapped) == true
            ? field.GetValue(wrapped)
            : null;
    }

    private static string? ResolveDisplayName(
        object? primary,
        object? secondary,
        object? fallbackOwner,
        string fallbackMemberName)
    {
        return GetNamedDisplayValue(primary)
            ?? GetNamedDisplayValue(secondary)
            ?? InvokeStringMember(fallbackOwner, fallbackMemberName)
            ?? GetTypeName(primary)
            ?? GetTypeName(secondary);
    }

    private static string? GetNamedDisplayValue(object? instance)
    {
        return InvokeStringMember(instance, "Name")
            ?? InvokeStringMember(instance, "GetName");
    }

    private static string? InvokeStringMember(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var instanceType = instance.GetType();
        var property = PropertyCache.GetOrAdd(
            $"{instanceType.AssemblyQualifiedName}|{memberName}",
            _ => instanceType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        if (property?.GetIndexParameters().Length == 0
            && property.PropertyType == typeof(string)
            && property.GetValue(instance) is string propertyValue
            && !string.IsNullOrWhiteSpace(propertyValue))
        {
            return propertyValue;
        }

        var method = MethodCache.GetOrAdd(
            $"{instanceType.AssemblyQualifiedName}|{memberName}",
            _ => instanceType.GetMethod(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        if (method is not null
            && method.GetParameters().Length == 0
            && method.ReturnType == typeof(string)
            && method.Invoke(instance, null) is string methodValue
            && !string.IsNullOrWhiteSpace(methodValue))
        {
            return methodValue;
        }

        return null;
    }

    private static string? GetTypeName(object? instance)
    {
        var typeName = instance?.GetType().Name;
        return string.IsNullOrWhiteSpace(typeName)
            ? null
            : typeName;
    }

    private static FieldInfo? ResolveField(string typeName, string fieldName)
    {
        var type = AccessTools.TypeByName(typeName);
        return type is null
            ? null
            : AccessTools.Field(type, fieldName);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class BotDebugBrainInspector
{
    public static string GetActiveLayerName(object botOwner)
    {
        return "None";
    }

    public static string GetActiveLogicName(object botOwner)
    {
        return "None";
    }
}
#endif

using System.Reflection;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerCombatRequestReflectionPolicy
{
    public static MethodInfo? ResolveTwoParameterMethod(
        IEnumerable<MethodInfo> methods,
        string methodName,
        Type firstArgumentType,
        Type secondArgumentType)
    {
        return methods
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .Where(method => MethodMatches(method, firstArgumentType, secondArgumentType))
            .FirstOrDefault();
    }

    public static bool WasInvocationSuccessful(MethodInfo? method, object? result)
    {
        if (method is null)
        {
            return false;
        }

        if (method.ReturnType == typeof(void))
        {
            return true;
        }

        return result is bool success && success;
    }

    private static bool MethodMatches(MethodInfo method, Type firstArgumentType, Type secondArgumentType)
    {
        if (method.ReturnType is not null
            && method.ReturnType != typeof(void)
            && method.ReturnType != typeof(bool))
        {
            return false;
        }

        var parameters = method.GetParameters();
        return parameters.Length == 2
            && parameters[0].ParameterType.IsAssignableFrom(firstArgumentType)
            && parameters[1].ParameterType.IsAssignableFrom(secondArgumentType);
    }
}

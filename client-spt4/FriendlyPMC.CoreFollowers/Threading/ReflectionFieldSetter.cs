using System.Reflection;

namespace FriendlyPMC.CoreFollowers.Threading;

public static class ReflectionFieldSetter
{
    public static bool TrySet(FieldInfo? field, object? target, object? value)
    {
        if (field is null || target is null)
        {
            return false;
        }

        field.SetValue(target, value);
        return true;
    }
}

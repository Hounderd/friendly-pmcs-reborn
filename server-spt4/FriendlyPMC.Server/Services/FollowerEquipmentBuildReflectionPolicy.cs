using FriendlyPMC.Server.Models;
using SPTarkov.Server.Core.Models.Common;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace FriendlyPMC.Server.Services;

public static class FollowerEquipmentBuildReflectionPolicy
{
    public static IReadOnlyList<string> ResolveBuildNames(object? fullProfile)
    {
        return ResolveEquipmentBuilds(fullProfile)
            .Select(build => ReadStringValue(build, "Name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool TryCreateEquipmentSnapshot(object? fullProfile, string buildName, out FollowerEquipmentSnapshot snapshot)
    {
        snapshot = null!;
        if (string.IsNullOrWhiteSpace(buildName))
        {
            return false;
        }

        var build = ResolveEquipmentBuilds(fullProfile)
            .FirstOrDefault(candidate => string.Equals(
                ReadStringValue(candidate, "Name"),
                buildName.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (build is null)
        {
            return false;
        }

        var items = ResolveBuildItems(build)
            .Select(CreateEquipmentItemSnapshot)
            .Where(item => item is not null)
            .Cast<FollowerEquipmentItemSnapshot>()
            .ToArray();
        if (items.Length == 0)
        {
            return false;
        }

        var equipmentId = ReadStringValue(build, "Root")
            ?? ReadStringValue(build, "EquipmentId")
            ?? ResolveEquipmentRootId(items)
            ?? items[0].Id;
        snapshot = new FollowerEquipmentSnapshot(equipmentId, items);
        return true;
    }

    internal static string? ResolveEquipmentRootId(IReadOnlyList<FollowerEquipmentItemSnapshot> items)
    {
        if (items.Count == 0)
        {
            return null;
        }

        var itemIds = items
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(item.ParentId)
                || !itemIds.Contains(item.ParentId))?
            .Id;
    }

    private static IEnumerable<object> ResolveEquipmentBuilds(object? fullProfile)
    {
        var userBuilds = ReadValue(fullProfile, "userbuilds")
            ?? ReadValue(fullProfile, "UserBuilds");
        var equipmentBuilds = ReadValue(userBuilds, "equipmentBuilds")
            ?? ReadValue(userBuilds, "EquipmentBuilds");
        return AsObjectEnumerable(equipmentBuilds);
    }

    private static IEnumerable<object> ResolveBuildItems(object? build)
    {
        var items = ReadValue(build, "Items")
            ?? ReadValue(build, "items");
        return AsObjectEnumerable(items);
    }

    private static FollowerEquipmentItemSnapshot? CreateEquipmentItemSnapshot(object item)
    {
        var id = ReadStringValue(item, "_id")
            ?? ReadStringValue(item, "Id");
        var templateId = ReadStringValue(item, "_tpl")
            ?? ReadStringValue(item, "Template")
            ?? ReadStringValue(item, "TemplateId");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(templateId))
        {
            return null;
        }

        return new FollowerEquipmentItemSnapshot(
            id,
            templateId,
            ReadStringValue(item, "parentId") ?? ReadStringValue(item, "ParentId"),
            ReadStringValue(item, "slotId") ?? ReadStringValue(item, "SlotId"),
            SerializeNode(ReadValue(item, "location") ?? ReadValue(item, "Location")),
            SerializeNode(ReadValue(item, "upd") ?? ReadValue(item, "Upd")));
    }

    private static IEnumerable<object> AsObjectEnumerable(object? value)
    {
        if (value is null || value is string)
        {
            return Array.Empty<object>();
        }

        if (value is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.Array => json.EnumerateArray().Select(element => (object)element).ToArray(),
                JsonValueKind.Object => [json],
                _ => Array.Empty<object>(),
            };
        }

        if (value is IEnumerable enumerable)
        {
            return enumerable.Cast<object>();
        }

        return Array.Empty<object>();
    }

    private static object? ReadValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        if (instance is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is string key
                    && string.Equals(key, memberName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        if (instance is JsonElement json)
        {
            foreach (var jsonProperty in json.EnumerateObject())
            {
                if (string.Equals(jsonProperty.Name, memberName, StringComparison.OrdinalIgnoreCase))
                {
                    return jsonProperty.Value;
                }
            }

            return null;
        }

        var type = instance.GetType();
        var reflectedProperty = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (reflectedProperty is not null)
        {
            return reflectedProperty.GetValue(instance);
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return field?.GetValue(instance);
    }

    private static string? ReadStringValue(object? instance, string memberName)
    {
        var value = ReadValue(instance, memberName);
        return value switch
        {
            null => null,
            string stringValue when !string.IsNullOrWhiteSpace(stringValue) => stringValue,
            MongoId mongoId => mongoId.ToString(),
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
            _ => value.ToString(),
        };
    }

    private static string? SerializeNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement jsonElement)
        {
            return jsonElement.GetRawText();
        }

        return JsonSerializer.Serialize(value);
    }
}

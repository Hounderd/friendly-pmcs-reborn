using SPTarkov.Server.Core.Models.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FriendlyPMC.Server.Services;

public static class FollowerHttpPayloadNormalizer
{
    private static readonly object OmitProperty = new();
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static object? Normalize(object? value, string? memberId = null)
    {
        if (value is null)
        {
            return null;
        }

        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        return RepairPayloadShape(ConvertJsonElement(element, "$"), memberId);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new MongoIdJsonConverter());
        return options;
    }

    private static object? ConvertJsonElement(JsonElement element, string path)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .Select(property => new KeyValuePair<string, object?>(property.Name, NormalizeObjectProperty(path, property)))
                .Where(entry => !ReferenceEquals(entry.Value, OmitProperty))
                .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().Value,
                    StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray()
                .Select((child, index) => ConvertJsonElement(child, $"{path}[{index}]"))
                .ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ConvertJsonNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }

    private static object? NormalizeObjectProperty(string objectPath, JsonProperty property)
    {
        var propertyPath = $"{objectPath}.{property.Name}";
        if (property.Value.ValueKind == JsonValueKind.Null)
        {
            if (string.Equals(property.Name, "karmaValue", StringComparison.Ordinal))
            {
                return 0f;
            }

            if (string.Equals(property.Name, "Type", StringComparison.Ordinal)
                && objectPath.EndsWith(".Info", StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(property.Name, "Minimum", StringComparison.Ordinal)
                && IsCurrentMinMaxPath(objectPath))
            {
                return 0f;
            }

            if ((string.Equals(property.Name, "OverDamageReceivedMultiplier", StringComparison.Ordinal)
                    || string.Equals(property.Name, "EnvironmentDamageMultiplier", StringComparison.Ordinal))
                && IsCurrentMinMaxPath(objectPath))
            {
                return 1f;
            }

            if (IsHealthResourcePath(objectPath))
            {
                return OmitProperty;
            }

            return OmitProperty;
        }

        return ConvertJsonElement(property.Value, propertyPath);
    }

    private static object ConvertJsonNumber(JsonElement element)
    {
        if (element.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (element.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return element.GetDouble();
    }

    private static object? RepairPayloadShape(object? value, string? memberId)
    {
        return value switch
        {
            Dictionary<string, object?> dictionary => RepairDictionaryShape(dictionary, memberId),
            object?[] array => array.Select(item => RepairPayloadShape(item, memberId)).ToArray(),
            _ => value,
        };
    }

    private static Dictionary<string, object?> RepairDictionaryShape(Dictionary<string, object?> dictionary, string? memberId)
    {
        var repaired = dictionary.ToDictionary(
            entry => entry.Key,
            entry => RepairPayloadShape(entry.Value, memberId),
            StringComparer.Ordinal);

        if (TryGetValueIgnoreCase(repaired, "Info", out var infoValue)
            && infoValue is Dictionary<string, object?> info)
        {
            RepairInfoShape(info);
        }

        if (LooksLikeBotProfileRoot(repaired))
        {
            RepairProfileRootShape(repaired, memberId);
            StripInvalidCustomizationDogTag(repaired);
            RepairNotesShape(repaired);
        }

        return repaired;
    }

    private static bool LooksLikeBotProfileRoot(Dictionary<string, object?> dictionary)
    {
        return ContainsKeyIgnoreCase(dictionary, "Info")
            && ContainsKeyIgnoreCase(dictionary, "Inventory")
            && ContainsKeyIgnoreCase(dictionary, "Health");
    }

    private static void RepairProfileRootShape(Dictionary<string, object?> dictionary, string? memberId)
    {
        NormalizeRootScalarTypes(dictionary, memberId);
        EnsureObject(dictionary, "Customization");
        EnsureObject(dictionary, "Encyclopedia");
        EnsureArray(dictionary, "InsuredItems");
        EnsureObject(dictionary, "TaskConditionCounters");
        EnsureArray(dictionary, "Quests");
        EnsureObject(dictionary, "Achievements");
        EnsureObject(dictionary, "Prestige");
        EnsureObject(dictionary, "Variables");
        EnsureArray(dictionary, "Bonuses");
        EnsureObject(dictionary, "WishList");
        EnsureObject(dictionary, "CheckedMagazines");
        EnsureArray(dictionary, "CheckedChambers");
        EnsureObject(dictionary, "TradersInfo");
        EnsureObject(dictionary, "UnlockedInfo");
        EnsureObject(dictionary, "moneyTransferLimitData");

        RepairSkillsShape(dictionary);
        RepairHideoutShape(dictionary);
        RepairRagfairInfoShape(dictionary);
        RepairStatsShape(dictionary);
    }

    private static void NormalizeRootScalarTypes(Dictionary<string, object?> dictionary, string? memberId)
    {
        if (!string.IsNullOrWhiteSpace(memberId))
        {
            dictionary["aid"] = memberId;
            return;
        }

        if (TryGetValueIgnoreCase(dictionary, "aid", out var aidValue) && aidValue is not null and not string)
        {
            dictionary["aid"] = aidValue.ToString();
        }
    }

    private static void RepairNotesShape(Dictionary<string, object?> dictionary)
    {
        Dictionary<string, object?> notes;
        if (TryGetValueIgnoreCase(dictionary, "Notes", out var notesValue)
            && notesValue is Dictionary<string, object?> existingNotes)
        {
            notes = existingNotes;
        }
        else
        {
            notes = new Dictionary<string, object?>(StringComparer.Ordinal);
            dictionary["Notes"] = notes;
        }

        var noteEntries = TryGetArrayValueIgnoreCase(notes, "Notes")
            ?? TryGetArrayValueIgnoreCase(notes, "DataNotes")
            ?? Array.Empty<object?>();

        notes["Notes"] = noteEntries;
        notes["DataNotes"] = noteEntries;
    }

    private static void RepairSkillsShape(Dictionary<string, object?> dictionary)
    {
        var skills = GetOrCreateObject(dictionary, "Skills");
        EnsureArray(skills, "Common");
        EnsureArray(skills, "Mastering");
    }

    private static void RepairHideoutShape(Dictionary<string, object?> dictionary)
    {
        var hideout = GetOrCreateObject(dictionary, "Hideout");
        EnsureObject(hideout, "Production");
        EnsureObject(hideout, "Improvements");
        EnsureArray(hideout, "Areas");
        SetIfMissingOrNull(hideout, "Seed", string.Empty);
        EnsureObject(hideout, "Customization");
        EnsureObject(hideout, "MannequinPoses");
    }

    private static void RepairRagfairInfoShape(Dictionary<string, object?> dictionary)
    {
        var ragfairInfo = GetOrCreateObject(dictionary, "RagfairInfo");
        EnsureArray(ragfairInfo, "offers");
        SetIfMissingOrNull(ragfairInfo, "rating", 0f);
        SetIfMissingOrNull(ragfairInfo, "isRatingGrowing", false);
    }

    private static void RepairStatsShape(Dictionary<string, object?> dictionary)
    {
        var stats = GetOrCreateObject(dictionary, "Stats");
        var eft = GetOrCreateObject(stats, "Eft");
        EnsureObject(stats, "Arena");

        RepairDamageHistoryShape(eft);
    }

    private static void RepairDamageHistoryShape(Dictionary<string, object?> eft)
    {
        var damageHistory = GetOrCreateObject(eft, "DamageHistory");
        EnsureObject(damageHistory, "BodyParts");
    }

    private static void StripInvalidCustomizationDogTag(Dictionary<string, object?> root)
    {
        if (!TryGetValueIgnoreCase(root, "Customization", out var customizationValue)
            || customizationValue is not Dictionary<string, object?> customization
            || !TryGetValueIgnoreCase(customization, "DogTag", out var currentDogTag))
        {
            return;
        }

        if (currentDogTag is null)
        {
            customization.Remove("DogTag");
            return;
        }

        if (currentDogTag is not string currentDogTagString || string.IsNullOrWhiteSpace(currentDogTagString))
        {
            customization.Remove("DogTag");
            return;
        }

        if (TryInferDogTagItem(root, out var dogTagItemId, out var dogTagTemplateId)
            && (string.Equals(currentDogTagString, dogTagItemId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentDogTagString, dogTagTemplateId, StringComparison.OrdinalIgnoreCase)))
        {
            customization.Remove("DogTag");
        }
    }

    private static bool TryInferDogTagItem(
        Dictionary<string, object?> dictionary,
        out string? dogTagItemId,
        out string? dogTagTemplateId)
    {
        dogTagItemId = null;
        dogTagTemplateId = null;
        if (!TryGetValueIgnoreCase(dictionary, "Inventory", out var inventoryValue)
            || inventoryValue is not Dictionary<string, object?> inventory
            || !TryGetValueIgnoreCase(inventory, "Items", out var itemsValue)
            || itemsValue is not object?[] items)
        {
            return false;
        }

        foreach (var itemValue in items)
        {
            if (itemValue is not Dictionary<string, object?> item)
            {
                continue;
            }

            if (!TryGetValueIgnoreCase(item, "slotId", out var slotIdValue)
                || slotIdValue is not string slotId
                || !string.Equals(slotId, "Dogtag", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryGetValueIgnoreCase(item, "_id", out var idValue)
                && idValue is string id
                && !string.IsNullOrWhiteSpace(id))
            {
                dogTagItemId = id;
            }

            if (TryGetValueIgnoreCase(item, "_tpl", out var templateValue)
                && templateValue is string templateId
                && !string.IsNullOrWhiteSpace(templateId))
            {
                dogTagTemplateId = templateId;
            }

            return !string.IsNullOrWhiteSpace(dogTagItemId) || !string.IsNullOrWhiteSpace(dogTagTemplateId);
        }

        return false;
    }

    private static void RepairInfoShape(Dictionary<string, object?> info)
    {
        var nickname = GetStringValue(info, "Nickname") ?? string.Empty;
        var side = GetStringValue(info, "Side") ?? "Usec";
        info["Nickname"] = nickname;
        info["Side"] = side;

        SetIfMissingOrNull(info, "MainProfileNickname", string.IsNullOrWhiteSpace(nickname) ? string.Empty : nickname);
        SetIfMissingOrNull(info, "EntryPoint", string.Empty);
        SetIfMissingOrNull(info, "GroupId", string.Empty);
        SetIfMissingOrNull(info, "TeamId", string.Empty);
        SetIfMissingOrNull(info, "GameVersion", string.Empty);
        SetIfMissingOrNull(info, "RegistrationDate", 0);
        SetIfMissingOrNull(info, "SavageLockTime", 0d);
        SetIfMissingOrNull(info, "NicknameChangeDate", 0L);
        SetIfMissingOrNull(info, "HasCoopExtension", false);
        SetIfMissingOrNull(info, "HasPveGame", false);
        SetIfMissingOrNull(info, "IsStreamerModeAvailable", false);
        SetIfMissingOrNull(info, "SquadInviteRestriction", false);
        SetIfMissingOrNull(info, "LockedMoveCommands", false);
        SetIfMissingOrNull(info, "MemberCategory", 0);
        SetIfMissingOrNull(info, "SelectedMemberCategory", 0);
        SetIfMissingOrNull(info, "PrestigeLevel", 0);

        if (!TryGetValueIgnoreCase(info, "Bans", out var bansValue) || bansValue is null)
        {
            info["Bans"] = Array.Empty<object?>();
        }

        Dictionary<string, object?> settings;
        if (TryGetValueIgnoreCase(info, "Settings", out var settingsValue)
            && settingsValue is Dictionary<string, object?> existingSettings)
        {
            settings = existingSettings;
        }
        else
        {
            settings = new Dictionary<string, object?>(StringComparer.Ordinal);
            info["Settings"] = settings;
        }

        SetIfMissingOrNull(settings, "Role", InferWildSpawnType(side));
        SetIfMissingOrNull(settings, "BotDifficulty", "normal");
        SetIfMissingOrNull(settings, "Experience", GetIntValue(info, "Experience") ?? 0);
        SetIfMissingOrNull(settings, "StandingForKill", 0d);
        SetIfMissingOrNull(settings, "AggressorBonus", 0d);
        SetIfMissingOrNull(settings, "UseSimpleAnimator", false);
    }

    private static string InferWildSpawnType(string side)
    {
        return string.Equals(side, "Bear", StringComparison.OrdinalIgnoreCase)
            ? "pmcBEAR"
            : "pmcUSEC";
    }

    private static void SetIfMissingOrNull(Dictionary<string, object?> dictionary, string key, object? value)
    {
        if (!TryGetValueIgnoreCase(dictionary, key, out var currentValue) || currentValue is null)
        {
            dictionary[key] = value;
        }
    }

    private static void EnsureObject(Dictionary<string, object?> dictionary, string key)
    {
        if (!TryGetValueIgnoreCase(dictionary, key, out var currentValue)
            || currentValue is not Dictionary<string, object?>)
        {
            dictionary[key] = new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }

    private static void EnsureArray(Dictionary<string, object?> dictionary, string key)
    {
        if (!TryGetValueIgnoreCase(dictionary, key, out var currentValue)
            || currentValue is not object?[])
        {
            dictionary[key] = Array.Empty<object?>();
        }
    }

    private static string? GetStringValue(Dictionary<string, object?> dictionary, string key)
    {
        return TryGetValueIgnoreCase(dictionary, key, out var value) && value is string stringValue
            ? stringValue
            : null;
    }

    private static int? GetIntValue(Dictionary<string, object?> dictionary, string key)
    {
        if (!TryGetValueIgnoreCase(dictionary, key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
            decimal decimalValue when decimalValue is >= int.MinValue and <= int.MaxValue => (int)decimalValue,
            double doubleValue when doubleValue is >= int.MinValue and <= int.MaxValue => (int)doubleValue,
            _ => null,
        };
    }

    private static object?[]? TryGetArrayValueIgnoreCase(Dictionary<string, object?> dictionary, string key)
    {
        return TryGetValueIgnoreCase(dictionary, key, out var value) && value is object?[] array
            ? array
            : null;
    }

    private static Dictionary<string, object?> GetOrCreateObject(Dictionary<string, object?> dictionary, string key)
    {
        if (TryGetValueIgnoreCase(dictionary, key, out var existingValue)
            && existingValue is Dictionary<string, object?> existingDictionary)
        {
            return existingDictionary;
        }

        var created = new Dictionary<string, object?>(StringComparer.Ordinal);
        dictionary[key] = created;
        return created;
    }

    private static bool TryGetValueIgnoreCase(Dictionary<string, object?> dictionary, string key, out object? value)
    {
        if (dictionary.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var entry in dictionary)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool ContainsKeyIgnoreCase(Dictionary<string, object?> dictionary, string key)
    {
        return TryGetValueIgnoreCase(dictionary, key, out _);
    }

    private static bool IsHealthResourcePath(string objectPath)
    {
        return objectPath.EndsWith(".Health.Hydration", StringComparison.Ordinal)
            || objectPath.EndsWith(".Health.Energy", StringComparison.Ordinal)
            || objectPath.EndsWith(".Health.Temperature", StringComparison.Ordinal)
            || objectPath.EndsWith(".Health.Poison", StringComparison.Ordinal);
    }

    private static bool IsCurrentMinMaxPath(string objectPath)
    {
        return IsHealthResourcePath(objectPath)
            || objectPath.EndsWith(".Health", StringComparison.Ordinal);
    }

    private sealed class MongoIdJsonConverter : JsonConverter<MongoId>
    {
        public override MongoId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("MongoId deserialization is not used for follower response normalization.");
        }

        public override MongoId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("MongoId deserialization is not used for follower response normalization.");
        }

        public override void Write(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(value.ToString());
        }
    }
}

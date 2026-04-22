using FriendlyPMC.Server.Models;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.DI.Annotations;
using System.Text.Json;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerProfileFactory
{
    public FollowerProfileSnapshot CreateEmpty(FollowerRosterRecord rosterRecord, int level = 0, int experience = 0)
    {
        return new FollowerProfileSnapshot(
            rosterRecord.Aid,
            rosterRecord.Nickname,
            rosterRecord.Side,
            level,
            experience,
            new Dictionary<string, int>(),
            Array.Empty<string>(),
            new FollowerHealthSnapshot(new Dictionary<string, HealthPartSnapshot>(StringComparer.Ordinal)),
            null,
            null);
    }

    public void ApplyPersistedSnapshot(BotBase generatedBot, FollowerProfileSnapshot snapshot)
    {
        generatedBot.Info ??= new Info();
        generatedBot.Info.Nickname = snapshot.Nickname;
        generatedBot.Info.Side = snapshot.Side;
        if (snapshot.Level > 0)
        {
            generatedBot.Info.Level = snapshot.Level;
            generatedBot.Info.Experience = snapshot.Experience;
        }

        if (snapshot.SkillProgress.Count > 0)
        {
            generatedBot.Skills ??= new Skills();
            var skills = (generatedBot.Skills.Common ?? Array.Empty<CommonSkill>()).ToList();
            foreach (var skillProgress in snapshot.SkillProgress)
            {
                if (!Enum.TryParse<SkillTypes>(skillProgress.Key, out var skillType))
                {
                    continue;
                }

                var skill = skills.FirstOrDefault(existing => existing.Id == skillType);
                if (skill is null)
                {
                    skill = new CommonSkill
                    {
                        Id = skillType,
                    };
                    skills.Add(skill);
                }

                skill.Progress = skillProgress.Value;
            }

            generatedBot.Skills.Common = skills;
        }

        if (snapshot.Health.Parts.Count > 0)
        {
            generatedBot.Health ??= new BotBaseHealth();
            generatedBot.Health.BodyParts ??= new Dictionary<string, BodyPartHealth>(StringComparer.Ordinal);
            foreach (var bodyPartSnapshot in snapshot.Health.Parts)
            {
                if (!generatedBot.Health.BodyParts.TryGetValue(bodyPartSnapshot.Key, out var bodyPartHealth))
                {
                    bodyPartHealth = new BodyPartHealth();
                    generatedBot.Health.BodyParts[bodyPartSnapshot.Key] = bodyPartHealth;
                }

                bodyPartHealth.Health ??= new CurrentMinMax();
                bodyPartHealth.Health.Current = bodyPartSnapshot.Value.Current;
                bodyPartHealth.Health.Maximum = bodyPartSnapshot.Value.Maximum;
            }
        }

        var persistedInventory = snapshot.Inventory ?? FollowerInventoryMigrationPolicy.CreateInventorySnapshot(snapshot.Equipment);
        if (persistedInventory is not null)
        {
            generatedBot.Inventory ??= new BotBaseInventory();
            generatedBot.Inventory.Equipment = new MongoId(persistedInventory.EquipmentId);
            generatedBot.Inventory.Items = persistedInventory.Items
                .Select(CreateInventoryItem)
                .ToList();
        }

        if (snapshot.Appearance is not null)
        {
            generatedBot.Customization ??= new Customization();
            ApplyCustomizationPart(snapshot.Appearance.Head, value => generatedBot.Customization.Head = value);
            ApplyCustomizationPart(snapshot.Appearance.Body, value => generatedBot.Customization.Body = value);
            ApplyCustomizationPart(snapshot.Appearance.Feet, value => generatedBot.Customization.Feet = value);
            ApplyCustomizationPart(snapshot.Appearance.Hands, value => generatedBot.Customization.Hands = value);
            ApplyCustomizationPart(snapshot.Appearance.Voice, value => generatedBot.Customization.Voice = value);
            ApplyCustomizationPart(snapshot.Appearance.DogTag, value => generatedBot.Customization.DogTag = value);
        }
    }

    internal static Item CreateInventoryItem(FollowerEquipmentItemSnapshot snapshot)
    {
        return CreateInventoryItem(new FollowerInventoryItemSnapshot(
            snapshot.Id,
            snapshot.TemplateId,
            snapshot.ParentId,
            snapshot.SlotId,
            snapshot.LocationJson,
            snapshot.UpdJson));
    }

    internal static Item CreateInventoryItem(FollowerInventoryItemSnapshot snapshot)
    {
        return new Item
        {
            Id = new MongoId(snapshot.Id),
            Template = new MongoId(snapshot.TemplateId),
            ParentId = snapshot.ParentId!,
            SlotId = snapshot.SlotId!,
            Location = DeserializeLocation(snapshot.LocationJson),
            Upd = DeserializeUpd(snapshot.UpdJson),
        };
    }

    internal static object? DeserializeLocation(string? locationJson)
    {
        if (string.IsNullOrWhiteSpace(locationJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(locationJson);
        return ConvertJsonElement(document.RootElement);
    }

    internal static Upd? DeserializeUpd(string? updJson)
    {
        if (string.IsNullOrWhiteSpace(updJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Upd>(updJson);
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => ConvertJsonElement(property.Value),
                    StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonElement)
                .ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ConvertJsonNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
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

    private static void ApplyCustomizationPart(string? value, Action<MongoId?> setter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        setter(new MongoId(value));
    }
}

using FriendlyPMC.Server.Models.Responses;
using SPTarkov.Server.Core.Utils;
using System.Text.Json;

namespace FriendlyPMC.Server.Services;

public static class FollowerPayloadProbeBuilder
{
    private static readonly string[] RequiredClientRootKeys =
    [
        "_id",
        "aid",
        "karmaValue",
        "Info",
        "Customization",
        "Encyclopedia",
        "Health",
        "Inventory",
        "InsuredItems",
        "Skills",
        "Notes",
        "TaskConditionCounters",
        "Quests",
        "Achievements",
        "Prestige",
        "Variables",
        "UnlockedInfo",
        "moneyTransferLimitData",
        "Bonuses",
        "Hideout",
        "RagfairInfo",
        "WishList",
        "Stats",
        "CheckedMagazines",
        "CheckedChambers",
        "TradersInfo",
    ];

    public static ProbeFollowerGeneratePayloadResponse Build(string sessionId, string memberId, object? normalizedPayload, JsonUtil jsonUtil)
    {
        var serializedJson = jsonUtil.Serialize(normalizedPayload, indented: true) ?? "null";
        using var document = JsonDocument.Parse(serializedJson);

        var root = document.RootElement;
        var botCount = root.ValueKind == JsonValueKind.Array ? root.GetArrayLength() : 0;
        var firstBot = botCount > 0 ? root[0] : default;
        var rootKeys = firstBot.ValueKind == JsonValueKind.Object
            ? firstBot.EnumerateObject().Select(property => property.Name).ToArray()
            : Array.Empty<string>();

        var missingClientRootKeys = RequiredClientRootKeys
            .Where(expected => !rootKeys.Contains(expected, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var nullPaths = new List<string>();
        if (firstBot.ValueKind != JsonValueKind.Undefined)
        {
            CollectNullPaths(firstBot, "$[0]", nullPaths);
        }

        return new ProbeFollowerGeneratePayloadResponse(
            sessionId,
            memberId,
            botCount,
            serializedJson,
            rootKeys,
            missingClientRootKeys,
            nullPaths,
            null);
    }

    public static ProbeFollowerGeneratePayloadResponse BuildError(string sessionId, string memberId, string error)
    {
        return new ProbeFollowerGeneratePayloadResponse(
            sessionId,
            memberId,
            0,
            "[]",
            Array.Empty<string>(),
            RequiredClientRootKeys,
            Array.Empty<string>(),
            error);
    }

    private static void CollectNullPaths(JsonElement element, string path, List<string> nullPaths)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectNullPaths(property.Value, $"{path}.{property.Name}", nullPaths);
                }
                break;

            case JsonValueKind.Array:
                for (var index = 0; index < element.GetArrayLength(); index++)
                {
                    CollectNullPaths(element[index], $"{path}[{index}]", nullPaths);
                }
                break;

            case JsonValueKind.Null:
                nullPaths.Add(path);
                break;
        }
    }
}

using FriendlyPMC.Server.Models.Requests;
using System.Collections;
using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Helpers;

namespace FriendlyPMC.Server.Services;

public interface IFollowerBotGenerator
{
    Task<IReadOnlyList<BotBase>> GenerateAsync(MongoId sessionId, GenerateBotsRequestData request);
    Task<IReadOnlyList<BotBase>> GenerateProbeAsync(MongoId sessionId, GenerateCondition condition, string location);
}

[Injectable]
public sealed class FollowerBotGenerator(BotController botController, ProfileHelper profileHelper) : IFollowerBotGenerator
{
    private static readonly MethodInfo GetBotGenerationDetailsForWaveMethod =
        typeof(BotController).GetMethod("GetBotGenerationDetailsForWave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("BotController.GetBotGenerationDetailsForWave was not found.");

    private static readonly FieldInfo BotGeneratorField =
        typeof(BotController).GetField("<botGenerator>P", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("BotController.botGenerator field was not found.");

    private static readonly MethodInfo PrepareAndGenerateBotMethod =
        BotGeneratorField.FieldType.GetMethod("PrepareAndGenerateBot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("BotGenerator.PrepareAndGenerateBot was not found.");

    public async Task<IReadOnlyList<BotBase>> GenerateAsync(MongoId sessionId, GenerateBotsRequestData request)
    {
        return (await botController.Generate(sessionId, request)).OfType<BotBase>().ToArray();
    }

    public Task<IReadOnlyList<BotBase>> GenerateProbeAsync(MongoId sessionId, GenerateCondition condition, string location)
    {
        var probeRaidConfiguration = new GetRaidConfigurationRequestData
        {
            Location = location,
            IsNightRaid = false,
            MaxGroupCount = Math.Max(1, condition.Limit),
            BotSettings = new BotSettings(),
            WavesSettings = new WavesSettings(),
        };

        var botGenerationDetails = GetBotGenerationDetailsForWaveMethod.Invoke(
            botController,
            [condition, profileHelper.GetPmcProfile(sessionId), false, probeRaidConfiguration])
            ?? throw new InvalidOperationException("BotController probe generation details were null.");

        var botGenerator = BotGeneratorField.GetValue(botController)
            ?? throw new InvalidOperationException("BotController botGenerator dependency was null.");
        var generatedBot = (BotBase?)PrepareAndGenerateBotMethod.Invoke(botGenerator, [sessionId, botGenerationDetails]);
        if (generatedBot is null)
        {
            return Task.FromResult<IReadOnlyList<BotBase>>(Array.Empty<BotBase>());
        }

        var side = generatedBot.Info?.Side;
        if (side is "Bear" or "Usec")
        {
            generatedBot.Info!.Side = "Savage";
        }

        return Task.FromResult<IReadOnlyList<BotBase>>([generatedBot]);
    }
}

[Injectable(InjectionType.Singleton)]
public sealed class FollowerBotGenerationService(
    FollowerManagerService managerService,
    FollowerProfileFactory profileFactory,
    IFollowerBotGenerator botGenerator)
{
    public async Task<IReadOnlyList<BotBase>> GenerateFollowerBotsAsync(MongoId sessionId, GenerateFollowerBotsRequest request)
    {
        if (request.Info is null)
        {
            return Array.Empty<BotBase>();
        }

        var generatedBots = (await botGenerator.GenerateAsync(sessionId, request.Info)).ToArray();

        if (generatedBots.Length == 0 || string.IsNullOrWhiteSpace(request.MemberId))
        {
            return generatedBots;
        }

        var persistedFollower = await managerService.TryGetFollowerForRaidAsync(sessionId.ToString(), request.MemberId);

        if (persistedFollower is null)
        {
            return generatedBots;
        }

        profileFactory.ApplyPersistedSnapshot(generatedBots[0], persistedFollower);
        return generatedBots;
    }

    public async Task<IReadOnlyList<BotBase>> GenerateFollowerBotProbeAsync(
        MongoId sessionId,
        string memberId,
        string role,
        string difficulty,
        int limit,
        string location)
    {
        var generatedBots = (await botGenerator.GenerateProbeAsync(
            sessionId,
            new GenerateCondition
            {
                Role = role,
                Limit = limit,
                Difficulty = difficulty,
            },
            location)).ToArray();

        if (generatedBots.Length == 0 || string.IsNullOrWhiteSpace(memberId))
        {
            return generatedBots;
        }

        var persistedFollower = await managerService.TryGetFollowerForRaidAsync(sessionId.ToString(), memberId);
        if (persistedFollower is null)
        {
            return generatedBots;
        }

        profileFactory.ApplyPersistedSnapshot(generatedBots[0], persistedFollower);
        return generatedBots;
    }
}

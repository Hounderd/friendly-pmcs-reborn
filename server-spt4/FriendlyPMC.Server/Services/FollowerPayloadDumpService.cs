using System.Reflection;
using FriendlyPMC.Server.Models.Responses;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Utils;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerPayloadDumpService
{
    private readonly string dumpDirectoryPath;
    private readonly JsonUtil jsonUtil;
    private readonly object sync = new();

    public FollowerPayloadDumpService(ModHelper modHelper, JsonUtil jsonUtil)
        : this(
            Path.Combine(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "data", "payload-dumps"),
            jsonUtil)
    {
    }

    public FollowerPayloadDumpService(string dumpDirectoryPath, JsonUtil jsonUtil)
    {
        this.dumpDirectoryPath = dumpDirectoryPath;
        this.jsonUtil = jsonUtil;
    }

    public void CaptureFollowerGeneratePayload(string sessionId, string? memberId, object? normalizedPayload)
    {
        try
        {
            Directory.CreateDirectory(dumpDirectoryPath);

            var resolvedMemberId = string.IsNullOrWhiteSpace(memberId) ? "unknown-member" : memberId;
            var probe = FollowerPayloadProbeBuilder.Build(sessionId, resolvedMemberId, normalizedPayload, jsonUtil);
            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var safeSessionId = SanitizeFileToken(sessionId);
            var safeMemberId = SanitizeFileToken(resolvedMemberId);
            var fileStem = $"followergenerate-{safeSessionId}-{safeMemberId}-{timestamp}";

            WriteText(Path.Combine(dumpDirectoryPath, "followergenerate-latest.raw.json"), probe.SerializedJson);
            WriteText(Path.Combine(dumpDirectoryPath, $"{fileStem}.raw.json"), probe.SerializedJson);

            var summaryJson = jsonUtil.Serialize(probe, indented: true) ?? "{}";
            WriteText(Path.Combine(dumpDirectoryPath, "followergenerate-latest.summary.json"), summaryJson);
            WriteText(Path.Combine(dumpDirectoryPath, $"{fileStem}.summary.json"), summaryJson);
        }
        catch
        {
            // Payload capture must never break the live follower generation route.
        }
    }

    private void WriteText(string path, string content)
    {
        lock (sync)
        {
            File.WriteAllText(path, content);
        }
    }

    private static string SanitizeFileToken(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var buffer = value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray();
        return new string(buffer);
    }
}

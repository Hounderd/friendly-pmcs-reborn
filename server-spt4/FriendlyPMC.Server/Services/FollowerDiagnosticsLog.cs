using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerDiagnosticsLog
{
    private readonly string logPath;
    private readonly object sync = new();

    public FollowerDiagnosticsLog(ModHelper modHelper)
        : this(Path.Combine(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "data", "diagnostics.log"))
    {
    }

    public FollowerDiagnosticsLog(string logPath)
    {
        this.logPath = logPath;
    }

    public void Append(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}";
            lock (sync)
            {
                File.AppendAllText(logPath, line);
            }
        }
        catch
        {
            // Diagnostics should never break the mod runtime.
        }
    }
}

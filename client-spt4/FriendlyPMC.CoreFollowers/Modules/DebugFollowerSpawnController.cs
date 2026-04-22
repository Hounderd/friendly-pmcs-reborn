namespace FriendlyPMC.CoreFollowers.Modules;

public interface IDebugFollowerSpawnService
{
    Task SpawnAsync();
}

public sealed class DebugFollowerSpawnController
{
    private readonly IDebugFollowerSpawnService spawnService;
    private bool isSpawnPending;
    private bool isSpawning;

    public DebugFollowerSpawnController(IDebugFollowerSpawnService spawnService)
    {
        this.spawnService = spawnService;
    }

    public void RequestSpawn()
    {
        isSpawnPending = true;
    }

    public async Task<bool> ProcessPendingSpawnAsync()
    {
        if (!isSpawnPending || isSpawning)
        {
            return false;
        }

        isSpawnPending = false;
        isSpawning = true;

        try
        {
            await spawnService.SpawnAsync();
            return true;
        }
        finally
        {
            isSpawning = false;
        }
    }
}

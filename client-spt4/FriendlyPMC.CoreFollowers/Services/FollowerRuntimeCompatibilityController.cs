namespace FriendlyPMC.CoreFollowers.Services;

public sealed class FollowerRuntimeCompatibilityController
{
    private readonly Func<FollowerLootingRuntimeDisableResult> disableLootingRuntime;
    private readonly Func<float> timeProvider;
    private readonly float lootingRetryIntervalSeconds;
    private readonly int maxLootingRetryAttempts;
    private float nextLootingRetryTime;
    private int lootingRetryAttempts;
    private bool lootingCompatibilitySatisfied;

    public FollowerRuntimeCompatibilityController(
        Func<FollowerLootingRuntimeDisableResult> disableLootingRuntime,
        Func<float>? timeProvider = null,
        float lootingRetryIntervalSeconds = 1f,
        int maxLootingRetryAttempts = 10)
    {
        this.disableLootingRuntime = disableLootingRuntime ?? throw new ArgumentNullException(nameof(disableLootingRuntime));
        this.timeProvider = timeProvider ?? (() => 0f);
        this.lootingRetryIntervalSeconds = lootingRetryIntervalSeconds;
        this.maxLootingRetryAttempts = maxLootingRetryAttempts;
    }

    public void Tick()
    {
        if (lootingCompatibilitySatisfied || lootingRetryAttempts >= maxLootingRetryAttempts)
        {
            return;
        }

        var now = timeProvider();
        if (now < nextLootingRetryTime)
        {
            return;
        }

        lootingRetryAttempts++;
        var result = disableLootingRuntime();
        lootingCompatibilitySatisfied = result.CompatibilitySatisfied;
        if (!lootingCompatibilitySatisfied)
        {
            nextLootingRetryTime = now + lootingRetryIntervalSeconds;
        }
    }
}

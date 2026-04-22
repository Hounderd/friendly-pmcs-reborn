namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct FollowerLootingRuntimeDisableResult(
    bool CompatibilitySatisfied,
    bool AppliedSuppression);

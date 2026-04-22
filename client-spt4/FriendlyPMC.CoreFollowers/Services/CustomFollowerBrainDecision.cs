namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerBrainDecision(
    CustomFollowerBrainMode Mode,
    bool PreferPlayerTarget);

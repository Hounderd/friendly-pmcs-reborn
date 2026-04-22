using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerDebugState(
    FollowerCommand Command,
    CustomFollowerBrainMode Mode,
    CustomFollowerNavigationIntent NavigationIntent,
    bool PreferPlayerTarget);

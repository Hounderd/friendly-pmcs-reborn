using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

public readonly record struct CustomFollowerReceiverState(
    FollowerCommand Command,
    bool HasHoldAnchor = false);

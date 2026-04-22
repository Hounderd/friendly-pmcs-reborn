namespace FriendlyPMC.Server.Models;

public enum FollowerCommandMode
{
    Follow,
    Hold,
    Regroup,
}

public sealed record FollowerCommandState(FollowerCommandMode Mode);

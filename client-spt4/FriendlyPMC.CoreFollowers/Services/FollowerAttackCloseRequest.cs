#if SPT_CLIENT
using EFT;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerAttackCloseRequest : BotRequest
{
    private readonly BotOwner owner;
    private readonly float expiresAtTimeSeconds;

    public FollowerAttackCloseRequest(BotOwner owner, Player requester)
        : base(requester, BotRequestType.attackClose)
    {
        this.owner = owner;
        expiresAtTimeSeconds = Time.time + 4f;
    }

    public override EBotRequestMode RequestMode => EBotRequestMode.Fight;

    public override bool CanProceed()
    {
        return Time.time <= expiresAtTimeSeconds
            && owner is not null
            && !owner.IsDead
            && Executor is not null
            && owner.Memory?.HaveEnemy == true;
    }

    public override bool CanRequest(BotOwner requester)
    {
        return true;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerAttackCloseRequest
{
}
#endif

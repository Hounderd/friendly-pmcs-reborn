#if SPT_CLIENT
using EFT;
using FriendlyPMC.CoreFollowers.Models;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class CustomFollowerMovementTargetPointPolicy
{
    public static BotDebugWorldPoint Resolve(
        Player requester,
        FollowerCommand command,
        FollowerMovementIntent movementIntent)
    {
        var playerPosition = requester.Transform.position;
        var forward = requester.LookDirection;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = requester.Transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward).normalized;
        var offset = FollowerOrderLayerPolicy.GetOffset(command, movementIntent);
        var target = playerPosition
            + (right * offset.X)
            + (Vector3.up * offset.Y)
            + (forward * offset.Z);

        return new BotDebugWorldPoint(target.x, target.y, target.z);
    }
}
#else
using FriendlyPMC.CoreFollowers.Models;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class CustomFollowerMovementTargetPointPolicy
{
    public static BotDebugWorldPoint Resolve(
        object requester,
        FollowerCommand command,
        FollowerMovementIntent movementIntent)
    {
        return new BotDebugWorldPoint(0f, 0f, 0f);
    }
}
#endif

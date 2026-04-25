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
        FollowerMovementIntent movementIntent,
        int formationSlotIndex = 0)
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
        var spacingProfile = ResolveSpacingProfile(requester);
        var offset = FollowerOrderLayerPolicy.GetOffset(command, movementIntent, formationSlotIndex, spacingProfile);
        var target = playerPosition
            + (right * offset.X)
            + (Vector3.up * offset.Y)
            + (forward * offset.Z);

        return new BotDebugWorldPoint(target.x, target.y, target.z);
    }

    private static FollowerFormationSpacingProfile ResolveSpacingProfile(Player requester)
    {
        var origin = requester.Transform.position + (Vector3.up * 0.5f);
        var hasOverheadHit = Physics.Raycast(
            origin,
            Vector3.up,
            out var hit,
            FollowerEnvironmentClassifierPolicy.IndoorCeilingProbeMeters,
            ~0,
            QueryTriggerInteraction.Ignore);

        return FollowerEnvironmentClassifierPolicy.Resolve(
            hasOverheadHit,
            hasOverheadHit ? hit.distance : float.MaxValue);
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
        FollowerMovementIntent movementIntent,
        int formationSlotIndex = 0)
    {
        return new BotDebugWorldPoint(0f, 0f, 0f);
    }
}
#endif

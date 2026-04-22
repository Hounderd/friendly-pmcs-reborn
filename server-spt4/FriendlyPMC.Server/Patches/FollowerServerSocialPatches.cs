using FriendlyPMC.Server.Services;
using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Profile;

namespace FriendlyPMC.Server.Patches;

public static class FollowerServerSocialPatches
{
    private const string HarmonyId = "xyz.pit.friendlypmc.server.social";
    private static bool applied;

    public static void Apply()
    {
        if (applied)
        {
            return;
        }

        applied = true;
        try
        {
            new Harmony(HarmonyId).PatchAll(typeof(FollowerServerSocialPatches).Assembly);
        }
        catch (Exception e)
        {
            FollowerServerHarmonyBridge.LogError("Failed to apply FollowerServerSocialPatches", e);
            throw; // Re-throw to ensure the server knows it failed
        }


    }
}

[HarmonyPatch(typeof(DialogueController), nameof(DialogueController.GetFriendList))]
internal static class DialogueControllerGetFriendListPatch
{
    private static void Postfix(MongoId sessionId, ref GetFriendListDataResponse __result)
    {
        try
        {
            FollowerServerHarmonyBridge.SocialViewService?
                .AppendRosterFriendsAsync(sessionId.ToString(), __result)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            FollowerServerHarmonyBridge.LogError("Failed to append follower manager friends", ex);
        }
    }
}

[HarmonyPatch(typeof(ProfileController), nameof(ProfileController.GetOtherProfile))]
internal static class ProfileControllerGetOtherProfilePatch
{
    private static bool Prefix(MongoId sessionId, GetOtherProfileRequest request, ref GetOtherProfileResponse __result)
    {
        try
        {
            var response = FollowerServerHarmonyBridge.SocialViewService?
                .TryBuildOtherProfileAsync(sessionId.ToString(), request.AccountId ?? string.Empty)
                .GetAwaiter()
                .GetResult();

            if (response is null)
            {
                return true;
            }

            __result = response;
            return false;
        }
        catch (Exception ex)
        {
            FollowerServerHarmonyBridge.LogError("Failed to build follower other-profile response", ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(DialogueController), nameof(DialogueController.DeleteFriend))]
internal static class DialogueControllerDeleteFriendPatch
{
    private static bool Prefix(MongoId sessionID, DeleteFriendRequest request)
    {
        try
        {
            var deleted = FollowerServerHarmonyBridge.SocialViewService?
                .TryDeleteFollowerByFriendIdAsync(sessionID.ToString(), request.FriendId.ToString())
                .GetAwaiter()
                .GetResult();

            return deleted != true;
        }
        catch (Exception ex)
        {
            FollowerServerHarmonyBridge.LogError("Failed to delete follower from friend list removal", ex);
            return true;
        }
    }
}

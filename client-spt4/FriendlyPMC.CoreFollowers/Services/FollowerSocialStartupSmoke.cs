#if SPT_CLIENT
using System.Collections;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerSocialStartupSmoke
{
    private const string FriendContext = "friend-context";
    private const EItemViewType ProfileViewType = (EItemViewType)25;
    private static readonly Type? ItemUiContextType = AccessTools.TypeByName("EFT.UI.ItemUiContext");
    private static readonly PropertyInfo? ItemUiContextInstanceProperty =
        ItemUiContextType is null ? null : AccessTools.Property(ItemUiContextType, "Instance");
    private static readonly MethodInfo? ShowPlayerProfileScreenMethod =
        ItemUiContextType is null ? null : AccessTools.Method(ItemUiContextType, "ShowPlayerProfileScreen", new[] { typeof(string), typeof(EItemViewType) });

    private static bool scheduled;

    public static void TrySchedule(IEnumerable<FollowerSocialStartupSmokeCandidate> candidates)
    {
        var plugin = FriendlyPmcCoreFollowersPlugin.Instance;
        if (!plugin.AutoSmokeFollowerProfileOnFriendHydrate || scheduled)
        {
            return;
        }

        var candidate = FollowerSocialStartupSmokePolicy.TrySelectCandidate(candidates);
        if (candidate is null)
        {
            return;
        }

        scheduled = true;
        var invocationAccountId = FollowerSocialStartupSmokePolicy.ResolveInvocationAccountId(candidate.Value);

        plugin.LogPluginInfo(
            $"Scheduling follower social startup smoke: nickname={candidate.Value.Nickname}, id={candidate.Value.Id}, requestedAccountId={candidate.Value.AccountId}, resolvedAccountId={invocationAccountId}");
        plugin.StartCoroutine(RunDelayedProfileOpen(candidate.Value, invocationAccountId));
    }

    private static IEnumerator RunDelayedProfileOpen(FollowerSocialStartupSmokeCandidate candidate, string resolvedAccountId)
    {
        yield return new WaitForSeconds(2f);

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var itemUiContext = ItemUiContextInstanceProperty?.GetValue(null);
            if (itemUiContext is null || ShowPlayerProfileScreenMethod is null)
            {
                FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                    $"Follower social startup smoke waiting for item ui context: attempt={attempt}, nickname={candidate.Nickname}, requestedAccountId={candidate.AccountId}");
                yield return new WaitForSeconds(1f);
                continue;
            }

            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Running follower social startup smoke: attempt={attempt}, nickname={candidate.Nickname}, id={candidate.Id}, requestedAccountId={candidate.AccountId}, resolvedAccountId={resolvedAccountId}");
            var taskObject = ShowPlayerProfileScreenMethod.Invoke(itemUiContext, new object[] { resolvedAccountId, ProfileViewType });
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Follower social startup smoke invoked profile open: requestedAccountId={candidate.AccountId}, resolvedAccountId={resolvedAccountId}, taskType={taskObject?.GetType().FullName ?? "<null>"}");
            yield break;
        }

        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower social startup smoke gave up waiting for item ui context: nickname={candidate.Nickname}, requestedAccountId={candidate.AccountId}");
    }
}
#endif

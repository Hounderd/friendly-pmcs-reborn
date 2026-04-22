#if SPT_CLIENT
using ChatShared;
using Comfort.Common;
using EFT.InventoryLogic;
using HarmonyLib;
using FriendlyPMC.CoreFollowers.Services;
using SPT.Reflection.Patching;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Patches;

internal static class FollowerSocialFriendRefresh
{
    private const string SquadManagerId = "67b0f29e151899410b04aacb";
    private static SocialNetworkClass? socialNetwork;
    private static IChatInteractions? chatInteractions;
    private static float nextRefreshTime;
    private static string lastLoggedSnapshot = string.Empty;

    public static void Bind(SocialNetworkClass network, IChatInteractions interactions)
    {
        socialNetwork = network;
        chatInteractions = interactions;
    }

    public static void RefreshNow()
    {
        if (socialNetwork is null || chatInteractions is null || Time.time < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.time + 2f;
        chatInteractions.GetFriendsList(new Callback<GClass1055>(socialNetwork.method_13));
    }

    public static async void RefreshSoon()
    {
        await Task.Delay(1500);
        RefreshNow();
    }

    public static void LogFriendSnapshot(SocialNetworkClass network)
    {
        try
        {
            var relevantFriends = network.FriendsList
                .Where(friend => friend is not null)
                .Select(friend => new
                {
                    friend.Id,
                    friend.AccountId,
                    Nickname = friend.Info?.Nickname ?? string.Empty,
                })
                .Where(friend =>
                    string.Equals(friend.Id, SquadManagerId, System.StringComparison.Ordinal)
                    || !string.IsNullOrWhiteSpace(friend.AccountId)
                    || !string.IsNullOrWhiteSpace(friend.Nickname))
                .OrderBy(friend => friend.Nickname, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(friend => friend.Id, System.StringComparer.Ordinal)
                .ToArray();

            if (relevantFriends.Length == 0)
            {
                return;
            }

            var snapshot = string.Join(
                " | ",
                relevantFriends.Select(friend =>
                    $"nickname={friend.Nickname},id={friend.Id},accountId={friend.AccountId}"));

            if (string.Equals(snapshot, lastLoggedSnapshot, System.StringComparison.Ordinal))
            {
                return;
            }

            lastLoggedSnapshot = snapshot;
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo($"Follower social snapshot: {snapshot}");
        }
        catch (System.Exception ex)
        {
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo($"Failed to log follower social snapshot: {ex.Message}");
        }
    }

    public static IReadOnlyList<FollowerInventoryFriendReference> GetFriendReferences()
    {
        if (socialNetwork?.FriendsList is null)
        {
            return Array.Empty<FollowerInventoryFriendReference>();
        }

        return socialNetwork.FriendsList
            .Where(friend => friend is not null)
            .Select(friend => new FollowerInventoryFriendReference(
                friend.Id ?? string.Empty,
                friend.AccountId ?? string.Empty,
                friend.Info?.Nickname ?? string.Empty))
            .ToArray();
    }

    public static bool ShouldRefresh(DialogueClass dialogue, ChatMessageClass message)
    {
        if (!string.Equals(dialogue.Profile.Id?.ToString(), SquadManagerId, System.StringComparison.Ordinal))
        {
            return false;
        }

        var text = message.Text?.Trim() ?? string.Empty;
        return text.StartsWith("/add ", System.StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("/rename ", System.StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("/delete ", System.StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("/autojoin ", System.StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("/kit ", System.StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("/equip ", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "/list", System.StringComparison.OrdinalIgnoreCase);
    }
}

internal static class FollowerSocialProfileOpenInterceptor
{
    private const string DialogueContext = "dialogue-context";
    private const string FriendContext = "friend-context";
    private const EItemViewType ProfileViewType = (EItemViewType)25;
    private static readonly Type? ItemUiContextType = AccessTools.TypeByName("EFT.UI.ItemUiContext");
    private static readonly PropertyInfo? ItemUiContextInstanceProperty =
        ItemUiContextType is null ? null : AccessTools.Property(ItemUiContextType, "Instance");
    private static readonly MethodInfo? ShowPlayerProfileScreenMethod =
        ItemUiContextType is null ? null : AccessTools.Method(ItemUiContextType, "ShowPlayerProfileScreen", new[] { typeof(string), typeof(EItemViewType) });
    private static readonly FieldInfo? UpdatableChatMemberAccountIdField =
        AccessTools.Field(typeof(UpdatableChatMember), "AccountId");

    private static readonly FieldInfo? DialogueInteractionDialogueField =
        AccessTools.Field(AccessTools.TypeByName("GClass3784")!, "DialogueClass");

    private static readonly FieldInfo? FriendInteractionMemberField =
        AccessTools.Field(AccessTools.TypeByName("GClass3785")!, "UpdatableChatMember_0");

    public static bool TryHandleDialogueProfileOpen(object instance)
    {
        var dialogue = DialogueInteractionDialogueField?.GetValue(instance) as DialogueClass;
        var member = dialogue?.Profile;
        var plan = FollowerSocialProfileOpenRequestPolicy.CreatePlan(
            DialogueContext,
            member?.AccountId,
            member?.Id,
            member?.AccountId);

        return TryHandleProfileOpen(plan, member?.Id, member?.AccountId, DialogueContext);
    }

    public static bool TryHandleFriendProfileOpen(object instance, out string? originalAccountId)
    {
        originalAccountId = null;
        var member = FriendInteractionMemberField?.GetValue(instance) as UpdatableChatMember;
        var plan = FollowerSocialProfileOpenRequestPolicy.CreatePlan(
            FriendContext,
            member?.AccountId,
            member?.Id,
            member?.AccountId);

        if (!TryLogRemappedProfileOpen(plan, member?.Id, member?.AccountId, FriendContext))
        {
            return false;
        }

        if (!plan.ShouldTemporarilyRewriteSelectedAccountId)
        {
            return false;
        }

        if (member is null
            || UpdatableChatMemberAccountIdField is null
            || string.IsNullOrWhiteSpace(plan.RequestedAccountId))
        {
            return TryHandleProfileOpen(plan, member?.Id, member?.AccountId, FriendContext);
        }

        UpdatableChatMemberAccountIdField.SetValue(member, plan.ResolvedAccountId);
        originalAccountId = plan.RequestedAccountId;
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Temporarily rewrote follower friend member account id for stock profile open: requestedAccountId={plan.RequestedAccountId}, resolvedAccountId={plan.ResolvedAccountId}, selectedId={member.Id}");
        return false;
    }

    public static void RestoreFriendProfileOpenAccountId(object instance, string? originalAccountId)
    {
        if (string.IsNullOrWhiteSpace(originalAccountId) || UpdatableChatMemberAccountIdField is null)
        {
            return;
        }

        var member = FriendInteractionMemberField?.GetValue(instance) as UpdatableChatMember;
        if (member is null)
        {
            return;
        }

        UpdatableChatMemberAccountIdField.SetValue(member, originalAccountId);
    }

    private static bool TryHandleProfileOpen(
        FollowerSocialProfileOpenPlan plan,
        string? selectedProfileId,
        string? selectedProfileAccountId,
        string source)
    {
        if (!TryLogRemappedProfileOpen(plan, selectedProfileId, selectedProfileAccountId, source))
        {
            return false;
        }

        if (!plan.ShouldHandleDirectly)
        {
            return false;
        }

        var itemUiContext = ItemUiContextInstanceProperty?.GetValue(null);
        if (itemUiContext is null)
        {
            return false;
        }

        if (ShowPlayerProfileScreenMethod?.Invoke(itemUiContext, new object[] { plan.ResolvedAccountId, ProfileViewType }) is not Task showProfileTask)
        {
            return false;
        }

        _ = showProfileTask
            .ContinueWith(
                continuationTask =>
                {
                    if (continuationTask.Exception is not null)
                    {
                        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginError(
                            $"Follower social menu profile open failed: source={source}, requestedAccountId={plan.RequestedAccountId}, resolvedAccountId={plan.ResolvedAccountId}",
                            continuationTask.Exception);
                    }
                },
                TaskContinuationOptions.OnlyOnFaulted);

        _ = showProfileTask
            .ContinueWith(
                _ =>
                {
                    FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                        $"Follower social menu profile open completed: source={source}, requestedAccountId={plan.RequestedAccountId}, resolvedAccountId={plan.ResolvedAccountId}");
                },
                TaskContinuationOptions.OnlyOnRanToCompletion);

        return true;
    }

    private static bool TryLogRemappedProfileOpen(
        FollowerSocialProfileOpenPlan plan,
        string? selectedProfileId,
        string? selectedProfileAccountId,
        string source)
    {
        if (string.IsNullOrWhiteSpace(plan.ResolvedAccountId))
        {
            return false;
        }

        if (!string.Equals(plan.ResolvedAccountId, plan.RequestedAccountId, System.StringComparison.Ordinal))
        {
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Remapped follower social menu profile open: source={source}, requestedAccountId={plan.RequestedAccountId}, selectedId={selectedProfileId}, selectedAccountId={selectedProfileAccountId}, resolvedAccountId={plan.ResolvedAccountId}");
        }

        return true;
    }
}

internal static class FollowerSocialProfileScreenDiagnostics
{
    private const string OtherPlayerProfileScreenControllerTypeName = "EFT.UI.OtherPlayerProfileScreen+GClass3883";

    private static readonly Type? OtherPlayerProfileScreenControllerType =
        AccessTools.TypeByName(OtherPlayerProfileScreenControllerTypeName);

    private static readonly Type? OtherPlayerProfileScreenControllerUiBaseType =
        ResolveOtherPlayerProfileScreenControllerUiBaseType();

    private static readonly FieldInfo? OtherPlayerProfileScreenControllerProfileField =
        OtherPlayerProfileScreenControllerType is null ? null : AccessTools.Field(OtherPlayerProfileScreenControllerType, "Profile");

    private static readonly FieldInfo? OtherPlayerProfileScreenProfileField =
        AccessTools.Field(AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen")!, "gclass1416_0");

    private static readonly FieldInfo? ProfileAccountIdField =
        AccessTools.Field(AccessTools.TypeByName("GClass1416")!, "AccountId");

    public static void LogScreenShow(object? controller)
    {
        var accountId = ReadAccountId(OtherPlayerProfileScreenControllerProfileField?.GetValue(controller));
        FollowerProfileScreenTracker.SetVisibleProfile(accountId);
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower profile screen show: accountId={accountId}");
    }

    public static void LogScreenClose(object? controller, bool moveForward)
    {
        var accountId = ReadAccountId(OtherPlayerProfileScreenControllerProfileField?.GetValue(controller));
        FollowerProfileScreenTracker.ClearVisibleProfile(accountId);
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower profile screen close action: accountId={accountId}, moveForward={moveForward}");
    }

    public static void LogScreenVisualShow(object? screen)
    {
        var accountId = ReadAccountId(OtherPlayerProfileScreenProfileField?.GetValue(screen));
        FollowerProfileScreenTracker.SetVisibleProfile(accountId);
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower profile screen visual show: accountId={accountId}");
    }

    public static void LogScreenDisplayStart(object? controller)
    {
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower profile screen display start: accountId={ReadAccountId(OtherPlayerProfileScreenControllerProfileField?.GetValue(controller))}");
    }

    public static void LogScreenShowStart(object? controller, object screenState)
    {
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower profile screen show start: accountId={ReadAccountId(OtherPlayerProfileScreenControllerProfileField?.GetValue(controller))}, screenState={screenState}");
    }

    public static void LogScreenShowAsyncStart(object? controller, object screenState, bool forced)
    {
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower profile screen show async start: accountId={ReadAccountId(OtherPlayerProfileScreenControllerProfileField?.GetValue(controller))}, screenState={screenState}, forced={forced}");
    }

    public static void LogScreenShowAsyncCompleted(object? controller, bool shown)
    {
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower profile screen show async completed: accountId={ReadAccountId(OtherPlayerProfileScreenControllerProfileField?.GetValue(controller))}, shown={shown}");
    }

    public static void LogScreenShowAsyncFailed(object? controller, System.Exception exception)
    {
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginError(
            $"Follower profile screen show async failed: accountId={ReadAccountId(OtherPlayerProfileScreenControllerProfileField?.GetValue(controller))}",
            exception);
    }

    public static void LogItemUiProfileOpenStart(string accountId, EItemViewType viewType)
    {
        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
            $"Follower item-ui profile open start: accountId={accountId}, viewType={viewType}");
    }

    public static void ObserveItemUiProfileOpenTask(string accountId, object? taskObject)
    {
        if (taskObject is not Task task)
        {
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Follower item-ui profile open returned non-task: accountId={accountId}, resultType={taskObject?.GetType().FullName ?? "<null>"}");
            return;
        }

        _ = task.ContinueWith(
            completedTask =>
            {
                try
                {
                    if (completedTask.IsCanceled)
                    {
                        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                            $"Follower item-ui profile open task canceled: accountId={accountId}, status={completedTask.Status}");
                        return;
                    }

                    if (completedTask.Exception is not null)
                    {
                        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginError(
                            $"Follower item-ui profile open task failed: accountId={accountId}",
                            completedTask.Exception);
                        return;
                    }

                    var resultObject = TryGetTaskResult(completedTask);
                    if (resultObject is null)
                    {
                        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                            $"Follower item-ui profile open task completed: accountId={accountId}, status={completedTask.Status}, controllerType=<null>, controllerProfileAccountId=<null>");
                        return;
                    }

                    var controllerProfile = OtherPlayerProfileScreenControllerProfileField?.GetValue(resultObject);
                    FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                        $"Follower item-ui profile open task completed: accountId={accountId}, status={completedTask.Status}, controllerType={resultObject?.GetType().FullName ?? "<null>"}, controllerProfileAccountId={ReadAccountId(controllerProfile)}");
                }
                catch (System.Exception ex)
                {
                    FriendlyPmcCoreFollowersPlugin.Instance.LogPluginError(
                        $"Failed to inspect follower item-ui profile open task: accountId={accountId}, status={completedTask.Status}",
                        ex);
                }
            });
    }

    public static void ObserveProfileEndpointResultTask(string accountId, object? taskObject)
    {
        if (taskObject is not Task task)
        {
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Follower profile endpoint returned non-task: accountId={accountId}, resultType={taskObject?.GetType().FullName ?? "<null>"}");
            return;
        }

        _ = task.ContinueWith(
            completedTask =>
            {
                try
                {
                    if (completedTask.IsCanceled)
                    {
                        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                            $"Follower profile endpoint task canceled: accountId={accountId}, status={completedTask.Status}");
                        return;
                    }

                    if (completedTask.Exception is not null)
                    {
                        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginError(
                            $"Follower profile endpoint task failed: accountId={accountId}",
                            completedTask.Exception);
                        return;
                    }

                    var resultObject = TryGetTaskResult(completedTask);
                    if (resultObject is null)
                    {
                        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                            $"Follower profile endpoint result missing: accountId={accountId}, status={completedTask.Status}");
                        return;
                    }

                    var failedProperty = resultObject.GetType().GetProperty("Failed", BindingFlags.Instance | BindingFlags.Public);
                    var errorProperty = resultObject.GetType().GetProperty("Error", BindingFlags.Instance | BindingFlags.Public);
                    var valueProperty = resultObject.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                    var failed = failedProperty?.GetValue(resultObject) as bool? ?? false;
                    if (failed)
                    {
                        FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                            $"Follower profile endpoint result failed: accountId={accountId}, error={errorProperty?.GetValue(resultObject) as string ?? "<null>"}");
                        return;
                    }

                    var valueObject = valueProperty?.GetValue(resultObject);
                    var descriptorAccountId = valueObject?.GetType()
                        .GetField("AccountId", BindingFlags.Instance | BindingFlags.Public)
                        ?.GetValue(valueObject) as string;
                    FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                        $"Follower profile endpoint result succeeded: accountId={accountId}, descriptorType={valueObject?.GetType().FullName ?? "<null>"}, descriptorAccountId={descriptorAccountId ?? "<null>"}");
                }
                catch (System.Exception ex)
                {
                    FriendlyPmcCoreFollowersPlugin.Instance.LogPluginError(
                        $"Failed to inspect follower profile endpoint result: accountId={accountId}, status={completedTask.Status}",
                        ex);
                }
            });
    }

    private static string ReadAccountId(object? profile)
    {
        return ProfileAccountIdField?.GetValue(profile) as string ?? "<null>";
    }

    private static Type? ResolveOtherPlayerProfileScreenControllerUiBaseType()
    {
        return OtherPlayerProfileScreenControllerType?.BaseType?.BaseType;
    }

    public static Type? GetOtherPlayerProfileScreenControllerUiBaseType()
    {
        return OtherPlayerProfileScreenControllerUiBaseType;
    }

    private static object? TryGetTaskResult(Task task)
    {
        var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
        return resultProperty?.GetValue(task);
    }
}

internal sealed class SocialNetworkClassInitPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(SocialNetworkClass), "method_1");
    }

    [PatchPostfix]
    private static void PatchPostfix(SocialNetworkClass __instance, IChatInteractions session, InventoryController inventoryController, string matchingVersion)
    {
        FollowerSocialFriendRefresh.Bind(__instance, session);
    }
}

internal sealed class SocialNetworkClassSendPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(SocialNetworkClass), nameof(SocialNetworkClass.SendMessage));
    }

    [PatchPostfix]
    private static void PatchPostfix(SocialNetworkClass __instance, DialogueClass dialogue, EMessageType messageType, ChatMessageClass message, System.Action callback)
    {
        if (FollowerSocialFriendRefresh.ShouldRefresh(dialogue, message))
        {
            FollowerSocialFriendRefresh.RefreshSoon();
        }
    }
}

internal sealed class SocialNetworkClassFriendsListHydratedPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(SocialNetworkClass), "method_13");
    }

    [PatchPostfix]
    private static void PatchPostfix(SocialNetworkClass __instance)
    {
        FollowerSocialFriendRefresh.LogFriendSnapshot(__instance);
        FollowerSocialStartupSmoke.TrySchedule(
            __instance.FriendsList
                .Where(friend => friend is not null)
                .Select(friend => new FollowerSocialStartupSmokeCandidate(
                    friend.Id ?? string.Empty,
                    friend.AccountId ?? string.Empty,
                    friend.Info?.Nickname ?? string.Empty)));
    }
}

internal sealed class DialogueInteractionProfileOpenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("GClass3784")!, "method_11");
    }

    [PatchPrefix]
    private static bool PatchPrefix(object __instance)
    {
        return !FollowerSocialProfileOpenInterceptor.TryHandleDialogueProfileOpen(__instance);
    }
}

internal sealed class FriendInteractionProfileOpenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("GClass3785")!, "method_15");
    }

    [PatchPrefix]
    private static bool PatchPrefix(object __instance, out string? __state)
    {
        return !FollowerSocialProfileOpenInterceptor.TryHandleFriendProfileOpen(__instance, out __state);
    }

    [PatchPostfix]
    private static void PatchPostfix(object __instance, string? __state)
    {
        FollowerSocialProfileOpenInterceptor.RestoreFriendProfileOpenAccountId(__instance, __state);
    }
}

internal sealed class ProfileEndpointGetOtherPlayerProfilePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ProfileEndpointFactoryAbstractClass), "GetOtherPlayerProfile");
    }

    [PatchPrefix]
    private static void PatchPrefix(ProfileEndpointFactoryAbstractClass __instance, ref string accountId)
    {
        try
        {
            var selectedProfile = __instance.SocialNetwork?.SelectedDialogue?.Profile;
            var remappedAccountId = FollowerSocialProfileRequestPolicy.TryRemapRequestedAccountId(
                accountId,
                __instance.Profile?.AccountId,
                selectedProfile?.Id,
                selectedProfile?.AccountId);

            if (string.IsNullOrWhiteSpace(remappedAccountId)
                || string.Equals(remappedAccountId, accountId, System.StringComparison.Ordinal))
            {
                return;
            }

            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Remapped follower profile request: requestedAccountId={accountId}, selectedId={selectedProfile?.Id}, selectedAccountId={selectedProfile?.AccountId}, remappedAccountId={remappedAccountId}");

            accountId = remappedAccountId;
        }
        catch (System.Exception ex)
        {
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo($"Failed to remap follower profile request: {ex.Message}");
        }
    }

    [PatchPostfix]
    private static void PatchPostfix(string accountId, object? __result)
    {
        FollowerSocialProfileScreenDiagnostics.ObserveProfileEndpointResultTask(accountId, __result);
    }
}

internal sealed class OtherPlayerProfileScreenShowPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen")!,
            "Show",
            new[] { AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen+GClass3883")! });
    }

    [PatchPostfix]
    private static void PatchPostfix(object __instance, object controller)
    {
        FollowerSocialProfileScreenDiagnostics.LogScreenShow(controller);
        FollowerSocialProfileScreenDiagnostics.LogScreenVisualShow(__instance);
        FollowerProfileActionInjector.TryInjectOpenInventoryAction(__instance, controller);
    }
}

internal sealed class ItemUiContextShowPlayerProfileScreenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var ItemUiContextType = AccessTools.TypeByName("EFT.UI.ItemUiContext")
            ?? throw new System.InvalidOperationException("Failed to resolve EFT.UI.ItemUiContext.");
        return AccessTools.Method(ItemUiContextType, "ShowPlayerProfileScreen", new[] { typeof(string), typeof(EItemViewType) });
    }

    [PatchPrefix]
    private static void PatchPrefix(string accId, EItemViewType viewType)
    {
        FollowerSocialProfileScreenDiagnostics.LogItemUiProfileOpenStart(accId, viewType);
    }

    [PatchPostfix]
    private static void PatchPostfix(string accId, object? __result)
    {
        FollowerSocialProfileScreenDiagnostics.ObserveItemUiProfileOpenTask(accId, __result);
    }
}

internal sealed class OtherPlayerProfileScreenVisualShowPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen")!,
            "Show",
            new[]
            {
                AccessTools.TypeByName("GClass1416")!,
                typeof(InventoryController),
                typeof(EItemViewType),
                AccessTools.TypeByName("ISession")!,
            });
    }

    [PatchPostfix]
    private static void PatchPostfix(object __instance)
    {
        FollowerSocialProfileScreenDiagnostics.LogScreenVisualShow(__instance);
    }
}

internal sealed class OtherPlayerProfileScreenControllerDisplayPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var OtherPlayerProfileScreenControllerUiBaseType = FollowerSocialProfileScreenDiagnostics.GetOtherPlayerProfileScreenControllerUiBaseType()
            ?? throw new System.InvalidOperationException("Failed to resolve OtherPlayerProfileScreen controller UI base type.");
        return AccessTools.Method(OtherPlayerProfileScreenControllerUiBaseType, "DisplayScreen");
    }

    [PatchPrefix]
    private static void PatchPrefix(object __instance)
    {
        FollowerSocialProfileScreenDiagnostics.LogScreenDisplayStart(__instance);
    }
}

internal sealed class OtherPlayerProfileScreenControllerShowScreenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var OtherPlayerProfileScreenControllerUiBaseType = FollowerSocialProfileScreenDiagnostics.GetOtherPlayerProfileScreenControllerUiBaseType()
            ?? throw new System.InvalidOperationException("Failed to resolve OtherPlayerProfileScreen controller UI base type.");
        return AccessTools.Method(OtherPlayerProfileScreenControllerUiBaseType, "ShowScreen", new[] { AccessTools.TypeByName("EFT.UI.Screens.EScreenState")! });
    }

    [PatchPrefix]
    private static void PatchPrefix(object __instance, object screenState)
    {
        FollowerSocialProfileScreenDiagnostics.LogScreenShowStart(__instance, screenState);
    }
}

internal sealed class OtherPlayerProfileScreenControllerShowAsyncPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var OtherPlayerProfileScreenControllerUiBaseType = FollowerSocialProfileScreenDiagnostics.GetOtherPlayerProfileScreenControllerUiBaseType()
            ?? throw new System.InvalidOperationException("Failed to resolve OtherPlayerProfileScreen controller UI base type.");
        return AccessTools.Method(OtherPlayerProfileScreenControllerUiBaseType, "ShowScreenAsync");
    }

    [PatchPrefix]
    private static void PatchPrefix(object __instance, object screenState, bool forced)
    {
        FollowerSocialProfileScreenDiagnostics.LogScreenShowAsyncStart(__instance, screenState, forced);
    }

    [PatchPostfix]
    private static void PatchPostfix(object __instance, Task<bool> __result)
    {
        _ = __result.ContinueWith(
            task =>
            {
                if (task.Exception is not null)
                {
                    FollowerSocialProfileScreenDiagnostics.LogScreenShowAsyncFailed(__instance, task.Exception);
                    return;
                }

                FollowerSocialProfileScreenDiagnostics.LogScreenShowAsyncCompleted(__instance, task.Result);
            });
    }
}

internal sealed class OtherPlayerProfileScreenControllerClosePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen+GClass3883")!, "CloseAction");
    }

    [PatchPrefix]
    private static void PatchPrefix(object __instance, bool moveForward)
    {
        FollowerSocialProfileScreenDiagnostics.LogScreenClose(__instance, moveForward);
    }
}
#endif

#if SPT_CLIENT
using System.Reflection;
using FriendlyPMC.CoreFollowers.Patches;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerProfileActionInjector
{
    private static readonly FieldInfo? HideoutButtonField =
        AccessTools.Field(AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen")!, "_hideoutButton");

    private static readonly Type? DefaultUIButtonType = AccessTools.TypeByName("EFT.UI.DefaultUIButton");

    private static readonly FieldInfo? DefaultUIButtonOnClickField =
        DefaultUIButtonType is null ? null : AccessTools.Field(DefaultUIButtonType, "OnClick");

    private static readonly PropertyInfo? DefaultUIButtonInteractableProperty =
        DefaultUIButtonType is null ? null : AccessTools.Property(DefaultUIButtonType, "Interactable");

    private static readonly MethodInfo? DefaultUIButtonSetHeaderTextMethod =
        DefaultUIButtonType is null ? null : AccessTools.Method(DefaultUIButtonType, "SetHeaderText", new[] { typeof(string) });

    private static readonly FieldInfo? ControllerProfileField =
        AccessTools.Field(AccessTools.TypeByName("EFT.UI.OtherPlayerProfileScreen+GClass3883")!, "Profile");

    private static readonly Type? ProfileType = AccessTools.TypeByName("GClass1416");

    private static readonly FieldInfo? ProfileAccountIdField =
        ProfileType is null ? null : AccessTools.Field(ProfileType, "AccountId");

    private static readonly PropertyInfo? ProfileNicknameProperty =
        ProfileType is null ? null : AccessTools.Property(ProfileType, "Nickname");

    public static void TryInjectOpenInventoryAction(object screen, object controller)
    {
        try
        {
            var profile = ControllerProfileField?.GetValue(controller);
            var viewedAccountId = ProfileAccountIdField?.GetValue(profile)?.ToString();
            var viewedNickname = ProfileNicknameProperty?.GetValue(profile)?.ToString();
            var decision = FollowerInventoryEntryPolicy.CreateDecision(
                viewedAccountId,
                viewedNickname,
                FollowerSocialFriendRefresh.GetFriendReferences());

            if (!decision.ShouldShowInventoryAction)
            {
                return;
            }

            var hideoutButtonObject = HideoutButtonField?.GetValue(screen);
            if (hideoutButtonObject is null
                || DefaultUIButtonType is null
                || !DefaultUIButtonType.IsInstanceOfType(hideoutButtonObject)
                || hideoutButtonObject is not Component hideoutButtonComponent)
            {
                FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                    $"Follower inventory action injection skipped: missing hideout button for follower={decision.Nickname}, aid={decision.FollowerAid}");
                return;
            }

            var onClick = DefaultUIButtonOnClickField?.GetValue(hideoutButtonObject) as UnityEvent;
            var unityButton = hideoutButtonComponent.gameObject.GetComponentInChildren<Button>(true);
            if (onClick is null && unityButton is null)
            {
                FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                    $"Follower inventory action injection skipped: missing click surfaces for follower={decision.Nickname}, aid={decision.FollowerAid}");
                return;
            }

            hideoutButtonComponent.gameObject.SetActive(true);
            SetInteractable(hideoutButtonObject, true);
            onClick?.RemoveAllListeners();
            unityButton?.onClick.RemoveAllListeners();

            var handler = hideoutButtonComponent.gameObject.GetComponent<FollowerProfileInventoryButtonHandler>()
                ?? hideoutButtonComponent.gameObject.AddComponent<FollowerProfileInventoryButtonHandler>();
            handler.Configure(hideoutButtonObject, hideoutButtonComponent, onClick, unityButton, decision.FollowerAid, decision.Nickname);
            handler.ResetLabel();

            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Injected follower inventory action into profile screen: follower={decision.Nickname}, aid={decision.FollowerAid}");
        }
        catch (Exception ex)
        {
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginError("Failed to inject follower inventory profile action", ex);
        }
    }

    internal static void SetButtonLabel(object hideoutButton, string label)
    {
        DefaultUIButtonSetHeaderTextMethod?.Invoke(hideoutButton, new object[] { label });
    }

    internal static void SetInteractable(object hideoutButton, bool isInteractable)
    {
        DefaultUIButtonInteractableProperty?.SetValue(hideoutButton, isInteractable);
    }

    private sealed class FollowerProfileInventoryButtonHandler : MonoBehaviour
    {
        private object? boundButton;
        private Component? boundButtonComponent;
        private UnityEvent? boundClickEvent;
        private Button? boundUnityButton;
        private string followerAid = string.Empty;
        private string nickname = string.Empty;
        private bool isHandlingClick;

        public void Configure(
            object button,
            Component buttonComponent,
            UnityEvent? clickEvent,
            Button? unityButton,
            string configuredFollowerAid,
            string configuredNickname)
        {
            if (boundButton != button || boundClickEvent != clickEvent || boundUnityButton != unityButton)
            {
                boundClickEvent?.RemoveListener(OnClick);
                boundUnityButton?.onClick.RemoveListener(OnClick);
                boundButton = button;
                boundButtonComponent = buttonComponent;
                boundClickEvent = clickEvent;
                boundUnityButton = unityButton;
            }

            boundClickEvent?.RemoveListener(OnClick);
            boundClickEvent?.AddListener(OnClick);
            boundUnityButton?.onClick.RemoveListener(OnClick);
            boundUnityButton?.onClick.AddListener(OnClick);

            followerAid = configuredFollowerAid;
            nickname = configuredNickname;
        }

        public void ResetLabel()
        {
            if (boundButton is null)
            {
                return;
            }

            FollowerProfileActionInjector.SetButtonLabel(boundButton, "OPEN INVENTORY");
        }

        private async void OnClick()
        {
            if (isHandlingClick || boundButton is null || boundButtonComponent is null)
            {
                return;
            }

            isHandlingClick = true;
            FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                $"Follower inventory button clicked: follower={nickname}, aid={followerAid}");
            FollowerProfileActionInjector.SetInteractable(boundButton, false);
            FollowerProfileActionInjector.SetButtonLabel(boundButton, "LOADING...");

            try
            {
                var state = await FriendlyPmcCoreFollowersPlugin.Instance.InventoryScreenController.OpenManagementAsync(
                    followerAid,
                    nickname,
                    boundButtonComponent.gameObject);
                var nextLabel = string.IsNullOrWhiteSpace(state.ErrorMessage) ? "OPEN INVENTORY" : "INVENTORY ERROR";
                FriendlyPmcCoreFollowersPlugin.Instance.LogPluginInfo(
                    $"Follower inventory button completed: follower={nickname}, aid={followerAid}, error={state.ErrorMessage ?? "<none>"}");
                FollowerProfileActionInjector.SetButtonLabel(boundButton, nextLabel);
            }
            catch (Exception ex)
            {
                FriendlyPmcCoreFollowersPlugin.Instance.LogPluginError(
                    $"Follower inventory button click failed: follower={nickname}, aid={followerAid}",
                    ex);
                FollowerProfileActionInjector.SetButtonLabel(boundButton, "INVENTORY ERROR");
            }
            finally
            {
                FollowerProfileActionInjector.SetInteractable(boundButton, true);
                isHandlingClick = false;
            }
        }
    }
}
#endif

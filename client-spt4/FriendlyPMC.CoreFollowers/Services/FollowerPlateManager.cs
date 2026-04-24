#if SPT_CLIENT
using EFT;
using FriendlyPMC.CoreFollowers.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerPlateManager : MonoBehaviour
{
    private const float SummaryIntervalSeconds = 5f;

    private readonly Dictionary<string, FollowerPlateView> activeViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FollowerPlateDiagnosticState> diagnosticStates = new(StringComparer.Ordinal);
    private readonly HashSet<string> eligibilityLoggedAids = new(StringComparer.Ordinal);
    private Canvas? overlayCanvas;
    private RectTransform? overlayRoot;
    private TMP_FontAsset? fontAsset;
    private float nextSummaryTime;
    private bool settingsLogged;

    private void Update()
    {
        var plugin = FriendlyPmcCoreFollowersPlugin.Instance;
        var localPlayer = GamePlayerOwner.MyPlayer;
        if (plugin is null || localPlayer is null)
        {
            HideAll();
            return;
        }

        if (!EnsureOverlayRoot())
        {
            HideAll();
            return;
        }

        var settings = plugin.PlateSettings;
        LogSettingsOnce(plugin, settings);
        if (!settings.Enabled)
        {
            HideAll();
            return;
        }

        var localPlayerPoint = BotDebugSnapshotMapper.GetWorldPoint(localPlayer);
        var liveAids = new HashSet<string>(StringComparer.Ordinal);
        var visiblePlateCount = 0;

        foreach (var follower in plugin.Registry.RuntimeFollowers)
        {
            liveAids.Add(follower.Aid);
            LogEligibilityOnce(plugin, follower);

            var distanceToPlayer = follower.CurrentPosition.DistanceTo(localPlayerPoint);
            var hiddenReason = FollowerPlateDiagnosticsPolicy.ResolveHiddenReason(
                settings.Enabled,
                follower.IsOperational,
                distanceToPlayer,
                settings.MaxDistanceMeters,
                projectionFailed: false);

            if (hiddenReason != FollowerPlateHiddenReason.None)
            {
                SetViewActive(follower.Aid, false);
                LogDiagnosticTransition(plugin, follower, new FollowerPlateDiagnosticState(false, hiddenReason));
                continue;
            }

            var snapshot = follower.CaptureSnapshot();
            var anchorPosition = follower.GetPlateAnchorPoint();
            var anchorVector = new Vector3(anchorPosition.X, anchorPosition.Y, anchorPosition.Z);

            if (!FollowerPlateProjection.TryProjectToCanvas(anchorVector, localPlayer, overlayRoot!, out var canvasPosition, out var failureReason))
            {
                SetViewActive(follower.Aid, false);
                LogDiagnosticTransition(
                    plugin,
                    follower,
                    new FollowerPlateDiagnosticState(false, FollowerPlateHiddenReason.ProjectionFailed),
                    failureReason);
                continue;
            }

            var view = GetOrCreateView(follower.Aid);
            view.SetActive(true);
            view.UpdateContent(
                snapshot.Nickname,
                snapshot.Side,
                follower.HealthPercent,
                settings);
            view.UpdatePosition(
                canvasPosition,
                FollowerPlateProjection.CalculateScale(distanceToPlayer, settings.MaxDistanceMeters, settings.Scale));
            visiblePlateCount++;
            LogDiagnosticTransition(plugin, follower, new FollowerPlateDiagnosticState(true, FollowerPlateHiddenReason.None));
        }

        CleanupStaleViews(liveAids);
        LogSummary(plugin, plugin.Registry.RuntimeFollowers.Count, visiblePlateCount);
    }

    private bool EnsureOverlayRoot()
    {
        if (overlayCanvas is not null && overlayRoot is not null)
        {
            return true;
        }

        var overlayCanvasObject = new GameObject(
            "FriendlyFollowerPlateCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        overlayCanvasObject.transform.SetParent(transform, false);

        overlayCanvas = overlayCanvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = short.MaxValue;

        var canvasScaler = overlayCanvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        var overlayObject = new GameObject("FriendlyFollowerPlates", typeof(RectTransform));
        overlayObject.transform.SetParent(overlayCanvasObject.transform, false);
        overlayRoot = overlayObject.GetComponent<RectTransform>();
        overlayRoot.anchorMin = Vector2.zero;
        overlayRoot.anchorMax = Vector2.one;
        overlayRoot.offsetMin = Vector2.zero;
        overlayRoot.offsetMax = Vector2.zero;
        overlayRoot.pivot = new Vector2(0.5f, 0.5f);

        fontAsset = FollowerPlateProjection.ResolveFont();
        FriendlyPmcCoreFollowersPlugin.Instance?.LogPluginInfo(
            $"Follower plate overlay created: canvas={overlayCanvasObject.name}, root={overlayObject.name}, font={(fontAsset is null ? "None" : fontAsset.name)}");
        return true;
    }

    private FollowerPlateView GetOrCreateView(string aid)
    {
        if (activeViews.TryGetValue(aid, out var existingView))
        {
            return existingView;
        }

        var view = FollowerPlateView.Create(overlayRoot!, fontAsset);
        activeViews[aid] = view;
        return view;
    }

    private void SetViewActive(string aid, bool active)
    {
        if (activeViews.TryGetValue(aid, out var view))
        {
            view.SetActive(active);
        }
    }

    private void HideAll()
    {
        foreach (var view in activeViews.Values)
        {
            view.SetActive(false);
        }
    }

    private void CleanupStaleViews(HashSet<string> liveAids)
    {
        var staleAids = activeViews.Keys.Where(aid => !liveAids.Contains(aid)).ToArray();
        foreach (var staleAid in staleAids)
        {
            if (diagnosticStates.TryGetValue(staleAid, out var state))
            {
                FriendlyPmcCoreFollowersPlugin.Instance?.LogPluginInfo(
                    $"Follower plate destroyed: aid={staleAid}, lastVisible={state.IsVisible}, lastReason={state.HiddenReason}");
                diagnosticStates.Remove(staleAid);
            }

            eligibilityLoggedAids.Remove(staleAid);
            activeViews[staleAid].Destroy();
            activeViews.Remove(staleAid);
        }
    }

    private void LogSettingsOnce(FriendlyPmcCoreFollowersPlugin plugin, FollowerPlateSettings settings)
    {
        if (!plugin.EnablePlateDiagnostics)
        {
            return;
        }

        if (settingsLogged)
        {
            return;
        }

        plugin.LogPluginInfo(
            $"Follower plate settings: enabled={settings.Enabled}, scale={settings.Scale}, maxDistance={settings.MaxDistanceMeters}, showHealthBar={settings.ShowHealthBar}, showHealthNumber={settings.ShowHealthNumber}, showFactionBadge={settings.ShowFactionBadge}, verticalOffset={settings.VerticalOffsetWorld}");
        settingsLogged = true;
    }

    private void LogEligibilityOnce(FriendlyPmcCoreFollowersPlugin plugin, IFollowerRuntimeHandle follower)
    {
        if (!plugin.EnablePlateDiagnostics)
        {
            return;
        }

        if (!eligibilityLoggedAids.Add(follower.Aid))
        {
            return;
        }

        var snapshot = follower.CaptureSnapshot();
        plugin.LogPluginInfo(
            FollowerPlateDiagnosticsPolicy.BuildEligibilityMessage(
                snapshot.Nickname,
                follower.Aid,
                snapshot.Side,
                follower.HealthPercent));
    }

    private void LogDiagnosticTransition(
        FriendlyPmcCoreFollowersPlugin plugin,
        IFollowerRuntimeHandle follower,
        FollowerPlateDiagnosticState state,
        FollowerPlateProjectionFailureReason failureReason = FollowerPlateProjectionFailureReason.None)
    {
        if (!plugin.EnablePlateDiagnostics)
        {
            return;
        }

        diagnosticStates.TryGetValue(follower.Aid, out var previousState);
        var hadPrevious = diagnosticStates.ContainsKey(follower.Aid);
        diagnosticStates[follower.Aid] = state;

        var snapshot = follower.CaptureSnapshot();
        var message = FollowerPlateDiagnosticsPolicy.BuildTransitionMessage(
            snapshot.Nickname,
            follower.Aid,
            hadPrevious ? previousState : null,
            state);
        if (message is null)
        {
            return;
        }

        if (failureReason != FollowerPlateProjectionFailureReason.None)
        {
            message = $"{message}, projectionFailure={failureReason}";
        }

        plugin.LogPluginInfo(message);
    }

    private void LogSummary(FriendlyPmcCoreFollowersPlugin plugin, int runtimeFollowerCount, int visiblePlateCount)
    {
        if (!plugin.EnablePlateDiagnostics)
        {
            return;
        }

        if (Time.unscaledTime < nextSummaryTime)
        {
            return;
        }

        var hiddenPlateCount = Math.Max(0, runtimeFollowerCount - visiblePlateCount);
        plugin.LogPluginInfo(
            FollowerPlateDiagnosticsPolicy.BuildSummaryMessage(
                runtimeFollowerCount,
                visiblePlateCount,
                hiddenPlateCount));
        nextSummaryTime = Time.unscaledTime + SummaryIntervalSeconds;
    }

    private void OnDestroy()
    {
        foreach (var view in activeViews.Values)
        {
            view.Destroy();
        }

        activeViews.Clear();
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerPlateManager
{
}
#endif

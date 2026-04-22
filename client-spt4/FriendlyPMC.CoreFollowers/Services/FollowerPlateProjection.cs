#if SPT_CLIENT
using EFT;
using EFT.Animations;
using TMPro;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal enum FollowerPlateProjectionFailureReason
{
    None = 0,
    NoCamera = 1,
    BehindCamera = 2,
}

internal static class FollowerPlateProjection
{
    public static bool TryProjectToCanvas(
        Vector3 worldPosition,
        Player mainPlayer,
        RectTransform canvasRect,
        out Vector2 canvasPosition,
        out FollowerPlateProjectionFailureReason failureReason)
    {
        canvasPosition = Vector2.zero;
        failureReason = FollowerPlateProjectionFailureReason.None;

        var camClass = CameraClass.Instance;
        if (mainPlayer is null || camClass?.Camera is null)
        {
            failureReason = FollowerPlateProjectionFailureReason.NoCamera;
            return false;
        }

        var projectionCamera = camClass.Camera;
        var canvasSize = canvasRect.rect.size;
        var scaleFactor = 1f;

        if (IsZoomedOpticAiming(mainPlayer.ProceduralWeaponAnimation))
        {
            var opticCamera = camClass.OpticCameraManager?.Camera;
            if (opticCamera is not null)
            {
                projectionCamera = opticCamera;
                canvasSize = opticCamera.pixelRect.max;
                scaleFactor = canvasRect.rect.width / Screen.width;
            }
        }

        var viewportPoint = projectionCamera.WorldToViewportPoint(worldPosition);
        if (viewportPoint.z <= 0f)
        {
            failureReason = FollowerPlateProjectionFailureReason.BehindCamera;
            return false;
        }

        canvasPosition = new Vector2(
            (viewportPoint.x - 0.5f) * canvasSize.x * scaleFactor,
            (viewportPoint.y - 0.5f) * canvasSize.y * scaleFactor);
        return true;
    }

    public static TMP_FontAsset ResolveFont()
    {
        return TMP_Settings.defaultFontAsset
               ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/BenderNormal SDF")
               ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    public static float CalculateScale(float distanceToPlayerMeters, float maxDistanceMeters, float baseScale)
    {
        var t = Mathf.InverseLerp(2f, maxDistanceMeters, distanceToPlayerMeters);
        return Mathf.Lerp(0.48f, 0.09f, t * Mathf.Sqrt(Mathf.Max(t, 0f))) * baseScale;
    }

    public static Color GetHealthColor(int healthPercent)
    {
        var clampedPercent = Mathf.Clamp01(healthPercent / 100f);
        return Color.Lerp(new Color(0.9f, 0.2f, 0.2f, 1f), new Color(0.2f, 0.95f, 0.35f, 1f), clampedPercent);
    }

    public static Color GetFactionColor(string side)
    {
        if (string.Equals(side, "Usec", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.22f, 0.62f, 1f, 0.95f);
        }

        if (string.Equals(side, "Bear", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.9f, 0.34f, 0.22f, 0.95f);
        }

        return new Color(0.6f, 0.6f, 0.6f, 0.95f);
    }

    private static bool IsZoomedOpticAiming(ProceduralWeaponAnimation? weaponAnimation)
    {
        if (weaponAnimation is null)
        {
            return false;
        }

        return weaponAnimation.IsAiming
               && weaponAnimation.CurrentScope is not null
               && weaponAnimation.CurrentScope.IsOptic
               && GetScopeZoomLevel(weaponAnimation) > 1f;
    }

    private static float GetScopeZoomLevel(ProceduralWeaponAnimation weaponAnimation)
    {
        var sight = weaponAnimation.CurrentAimingMod;
        if (sight is null)
        {
            return 1f;
        }

        return sight.ScopeZoomValue > 1f
            ? sight.ScopeZoomValue
            : sight.GetCurrentOpticZoom();
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerPlateProjection
{
}
#endif

#if SPT_CLIENT
using TMPro;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerInventoryRuntimeViewFactory : IFollowerInventoryRuntimeViewFactory
{
    public IFollowerInventoryRuntimeView Create(object? hostScreen, FollowerInventoryScreenActions actions)
    {
        return FollowerInventoryOverlayView.Create(hostScreen, ResolveFontAsset(), actions);
    }

    private static TMP_FontAsset? ResolveFontAsset()
    {
        return Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerInventoryRuntimeViewFactory : IFollowerInventoryRuntimeViewFactory
{
    public IFollowerInventoryRuntimeView Create(object? hostScreen, FollowerInventoryScreenActions actions)
    {
        return new NoopFollowerInventoryRuntimeView();
    }

    private sealed class NoopFollowerInventoryRuntimeView : IFollowerInventoryRuntimeView
    {
        public void Render(FollowerInventoryScreenViewModel model)
        {
        }

        public void Dispose()
        {
        }
    }
}
#endif

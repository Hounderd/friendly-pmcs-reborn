#if SPT_CLIENT
using Comfort.Common;
using EFT.UI.DragAndDrop;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.UI;

namespace FriendlyPMC.CoreFollowers.Services;

internal sealed class FollowerInventoryItemIconView : MonoBehaviour
{
    private Image? iconImage;
    private Action? releaseBinding;

    private void Awake()
    {
        iconImage = GetComponent<Image>();
        if (iconImage is not null)
        {
            iconImage.preserveAspect = true;
            iconImage.enabled = false;
        }
    }

    public void Bind(string itemId, string templateId)
    {
        ClearBinding();

        if (iconImage is null)
        {
            iconImage = GetComponent<Image>();
        }

        if (iconImage is null
            || string.IsNullOrWhiteSpace(itemId)
            || string.IsNullOrWhiteSpace(templateId)
            || !Singleton<ItemFactoryClass>.Instantiated)
        {
            ApplySprite(null);
            return;
        }

        try
        {
            var previewItem = Singleton<ItemFactoryClass>.Instance.CreateItem(itemId, templateId, null);
            var itemIcon = ItemViewFactory.LoadItemIcon(previewItem, 1, false);
            ApplySprite(itemIcon.Sprite);
            releaseBinding = itemIcon.Changed.Bind(() => ApplySprite(itemIcon.Sprite));
        }
        catch
        {
            ApplySprite(null);
        }
    }

    private void OnDestroy()
    {
        ClearBinding();
    }

    private void ClearBinding()
    {
        if (releaseBinding is not null)
        {
            releaseBinding.Invoke();
            releaseBinding = null;
        }
    }

    private void ApplySprite(Sprite? sprite)
    {
        if (iconImage is null)
        {
            return;
        }

        iconImage.sprite = sprite;
        iconImage.enabled = sprite is not null;
        iconImage.color = sprite is null
            ? new Color(0.3f, 0.35f, 0.42f, 0.45f)
            : Color.white;
    }
}
#endif

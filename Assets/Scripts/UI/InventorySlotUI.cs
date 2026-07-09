using UnityEngine;
using UnityEngine.UI;
using BokeGameJam.Core;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 显示玩家当前持有物图标与名称，订阅 <see cref="GameEvents.HeldItemChanged"/>。
    /// </summary>
    public sealed class InventorySlotUI : MonoBehaviour
    {
        public const string ResourceId = "InventorySlot";

        [SerializeField] private Image iconImage;
        [SerializeField] private Image slotBackground;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Color emptySlotColor = new(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color filledSlotColor = new(0.12f, 0.12f, 0.14f, 0.75f);
        [SerializeField] private bool preserveIconAspect = true;
        [SerializeField] private bool clearLabelWhenEmpty = true;

        private void OnEnable()
        {
            EventManager.On<HeldItemInfo>(GameEvents.HeldItemChanged, OnHeldItemChanged);
            Apply(HeldItemInfo.Empty);
        }

        private void OnDisable()
        {
            EventManager.Off<HeldItemInfo>(GameEvents.HeldItemChanged, OnHeldItemChanged);
        }

        private void OnHeldItemChanged(HeldItemInfo info)
        {
            Apply(info);
        }

        private void Apply(HeldItemInfo info)
        {
            bool hasItem = info.HasItem && info.Icon != null;

            if (iconImage != null)
            {
                iconImage.sprite = hasItem ? info.Icon : null;
                iconImage.enabled = hasItem;
                iconImage.preserveAspect = preserveIconAspect;
            }

            if (slotBackground != null)
                slotBackground.color = hasItem ? filledSlotColor : emptySlotColor;

            if (nameLabel != null)
            {
                if (info.HasItem && !string.IsNullOrEmpty(info.DisplayName))
                    nameLabel.text = info.DisplayName;
                else if (clearLabelWhenEmpty)
                    nameLabel.text = string.Empty;
            }
        }
    }
}

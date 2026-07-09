using UnityEngine;
using UnityEngine.UI;
using BokeGameJam.Core;
using BokeGameJam.Gameplay;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 单格物品栏 UI：挂在 InventorySlot 预制体根节点上，通过 UIManager 加载。
    /// 订阅 <see cref="GameEvents.HeldItemChanged"/> 刷新图标 / 名称。
    /// </summary>
    /// <remarks>
    /// 预制体搭建约定：
    /// 1. 根节点带 RectTransform + 本脚本。锚点建议左上/右上角。
    /// 2. 子节点：
    ///    - SlotBackground (可选): Image，作为底框，绑到 <see cref="slotBackground"/>
    ///    - IconImage (必需): Image，展示物品图标，绑到 <see cref="iconImage"/>
    ///    - NameLabel (可选): Text 或 TMP_Text，绑到 <see cref="nameLabel"/>；用于显示物品名
    /// 3. 在 ResourceDefinitionDatabase.uiPrefabs 里以 resourceId "InventorySlot" 登记该预制体。
    /// </remarks>
    public sealed class InventorySlotUI : MonoBehaviour
    {
        public const string ResourceId = "InventorySlot";

        [Header("Wiring (在预制体上拖引用)")]
        [Tooltip("展示物品图标的 Image；未持有物品时会自动隐藏。")]
        [SerializeField] private Image iconImage;

        [Tooltip("可选：格子的底框 Image。空/有物品时会切换颜色。")]
        [SerializeField] private Image slotBackground;

        [Tooltip("可选：物品名称文本。使用 UnityEngine.UI.Text。")]
        [SerializeField] private Text nameLabel;

        [Header("Colors")]
        [SerializeField] private Color emptySlotColor = new(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color filledSlotColor = new(0.12f, 0.12f, 0.14f, 0.75f);

        [Header("Behaviour")]
        [Tooltip("图标保持原比例。")]
        [SerializeField] private bool preserveIconAspect = true;

        [Tooltip("未持有物品时，是否把名称文本清空。")]
        [SerializeField] private bool clearLabelWhenEmpty = true;

        private void Awake()
        {
            if (iconImage == null)
                Debug.LogWarning("[InventorySlotUI] iconImage 未绑定，预制体请把 Icon 子节点的 Image 拖上来。", this);

            Refresh(HeldItemInfo.Empty);
        }

        private void OnEnable()
        {
            EventManager.On<HeldItemInfo>(GameEvents.HeldItemChanged, OnHeldItemChanged);
            SyncFromPlayer();
        }

        private void OnDisable()
        {
            EventManager.Off<HeldItemInfo>(GameEvents.HeldItemChanged, OnHeldItemChanged);
        }

        private void OnHeldItemChanged(HeldItemInfo info)
        {
            Debug.Log(
                $"[InventorySlotUI] HeldItemChanged hasItem={info.HasItem} name='{info.DisplayName}' icon={(info.Icon != null ? info.Icon.name : "null")}",
                this);
            Refresh(info);
        }

        /// <summary>UI 晚于玩家启用时，主动同步一次当前持有物。</summary>
        private void SyncFromPlayer()
        {
            PlayerHeldItem held = FindObjectOfType<PlayerHeldItem>();
            if (held != null && held.HasItem)
            {
                Refresh(new HeldItemInfo(true, held.HeldIcon, held.HeldDisplayName));
                return;
            }

            Refresh(HeldItemInfo.Empty);
        }

        public void Refresh(HeldItemInfo info)
        {
            bool has = info.HasItem && info.Icon != null;

            if (iconImage != null)
            {
                iconImage.enabled = has;
                iconImage.sprite = has ? info.Icon : null;
                iconImage.preserveAspect = preserveIconAspect;
            }

            if (slotBackground != null)
                slotBackground.color = has ? filledSlotColor : emptySlotColor;

            if (nameLabel != null)
            {
                if (has)
                    nameLabel.text = info.DisplayName ?? string.Empty;
                else if (clearLabelWhenEmpty)
                    nameLabel.text = string.Empty;
            }
        }
    }
}

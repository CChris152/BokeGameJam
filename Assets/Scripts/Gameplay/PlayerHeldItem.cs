using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 玩家单格持有槽：拾取后持有一件，再按 E 放下。
    /// </summary>
    public sealed class PlayerHeldItem : MonoBehaviour
    {
        [Header("Drop")]
        [SerializeField] private Vector2 dropOffset = new(0.6f, 0f);
        [SerializeField] private bool faceAwareDrop = true;

        private HeldItemData held;

        public bool HasItem => held.IsValid;
        public Sprite HeldIcon => held.Icon;
        public string HeldDisplayName => held.DisplayName;

        private void OnEnable()
        {
            EmitChanged();
        }

        public bool TryHold(HeldItemData item)
        {
            if (!item.IsValid || HasItem)
                return false;

            held = item;
            EmitChanged();
            return true;
        }

        public bool TryDrop()
        {
            if (!HasItem)
                return false;

            HeldItemData dropping = held;
            held = default;
            EmitChanged();

            Vector2 offset = dropOffset;
            if (faceAwareDrop && transform.localScale.x < 0f)
                offset.x = -Mathf.Abs(offset.x);

            Vector3 spawnPos = transform.position + (Vector3)offset;
            GameObject instance = Instantiate(dropping.DropPrefab, spawnPos, Quaternion.identity);
            instance.name = dropping.DropPrefab.name;
            return true;
        }

        public void Clear()
        {
            if (!HasItem)
                return;

            held = default;
            EmitChanged();
        }

        private void EmitChanged()
        {
            HeldItemInfo info = HasItem
                ? new HeldItemInfo(true, held.Icon, held.DisplayName)
                : HeldItemInfo.Empty;
            EventManager.Emit(GameEvents.HeldItemChanged, info);
        }
    }

    /// <summary>一次持有所需的数据。</summary>
    public struct HeldItemData
    {
        public Sprite Icon;
        public GameObject DropPrefab;
        public string DisplayName;

        public bool IsValid => DropPrefab != null;

        public HeldItemData(Sprite icon, GameObject dropPrefab, string displayName)
        {
            Icon = icon;
            DropPrefab = dropPrefab;
            DisplayName = displayName ?? string.Empty;
        }
    }
}

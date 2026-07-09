using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>玩家单格持有槽：拾取后持有一件，再按 E 放下。</summary>
    public sealed class PlayerHeldItem : MonoBehaviour
    {
        [Header("Drop")]
        [SerializeField] private Vector2 dropOffset = new(0.6f, 0f);
        [SerializeField] private bool faceAwareDrop = true;

        [Header("Debug")]
        [SerializeField] private bool traceLogging = true;

        private GameObject heldPrefab;
        private Sprite heldIcon;
        private string heldName;

        public bool HasItem => heldPrefab != null;
        public Sprite HeldIcon => heldIcon;
        public string HeldDisplayName => heldName;

        private void OnEnable() => EmitChanged();

        public bool TryHold(GameObject prefab, Sprite icon, string displayName)
        {
            Trace(
                $"TryHold begin holder={Describe(this)} prefab={Describe(prefab)} icon={Describe(icon)} displayName='{displayName ?? string.Empty}' hasItem={HasItem} currentPrefab={Describe(heldPrefab)}");

            if (prefab == null)
            {
                Trace("TryHold rejected: prefab is null.");
                return false;
            }

            if (HasItem)
            {
                Trace($"TryHold rejected: holder already has {Describe(heldPrefab)}.");
                return false;
            }

            heldPrefab = prefab;
            heldIcon = icon;
            heldName = displayName ?? string.Empty;
            Trace($"TryHold success holder={Describe(this)} heldPrefab={Describe(heldPrefab)} icon={Describe(heldIcon)} heldName='{heldName}'.");
            EmitChanged();
            return true;
        }

        public bool TryDrop()
        {
            Trace(
                $"TryDrop begin holder={Describe(this)} hasItem={HasItem} heldPrefab={Describe(heldPrefab)} position={transform.position} dropOffset={dropOffset} faceAwareDrop={faceAwareDrop} localScale={transform.localScale}");

            if (!HasItem)
            {
                Trace("TryDrop rejected: holder has no item.");
                return false;
            }

            Vector2 offset = dropOffset;
            if (faceAwareDrop && transform.localScale.x < 0f)
                offset.x = -Mathf.Abs(offset.x);

            GameObject instance = Instantiate(heldPrefab, transform.position + (Vector3)offset, Quaternion.identity);
            instance.name = heldPrefab.name;
            Trace($"TryDrop spawned instance={Describe(instance)} fromPrefab={Describe(heldPrefab)} spawnPosition={instance.transform.position}.");

            heldPrefab = null;
            heldIcon = null;
            heldName = null;
            Trace($"TryDrop cleared holder={Describe(this)} hasItem={HasItem}.");
            EmitChanged();
            return true;
        }

        private void EmitChanged()
        {
            HeldItemInfo info = HasItem ? new HeldItemInfo(true, heldIcon, heldName) : HeldItemInfo.Empty;
            Trace($"EmitChanged hasItem={info.HasItem} icon={Describe(info.Icon)} displayName='{info.DisplayName}'.");
            EventManager.Emit(GameEvents.HeldItemChanged, info);
        }

        private void Trace(string message)
        {
            if (traceLogging)
                Debug.Log($"[PlayerHeldItem] {message}", this);
        }

        private static string Describe(Object value)
        {
            return value != null ? $"{value.name}#{value.GetInstanceID()}" : "null";
        }
    }
}

using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>可拾取物体：Trigger Collider2D，进入范围高亮，按 E 拾取。</summary>
    [RequireComponent(typeof(Collider2D))]
    public class PickableObject : MonoBehaviour
    {
        private const string HighlightMaterialResourcePath = "Art/Materials/SpriteHighlight";
        private static readonly int HighlightId = Shader.PropertyToID("_Highlight");

        [Header("Pickup")]
        [Tooltip("拾取后进入玩家持有槽；放下时按此 prefab 重新生成。留空则用自身对应的 prefab 资源")]
        [SerializeField] private GameObject dropPrefab;

        [Tooltip("物品栏显示名；留空用物体名")]
        [SerializeField] private string displayName = string.Empty;

        [Tooltip("物品栏图标；留空用 SpriteRenderer.sprite")]
        [SerializeField] private Sprite iconOverride;

        [Header("Highlight")]
        [Tooltip("留空则从 Resources/Art/Materials/SpriteHighlight 加载")]
        [SerializeField] private Material highlightMaterial;

        [Header("Debug")]
        [SerializeField] private bool traceLogging = true;

        private bool isPickedUp;
        private bool isHighlighted;
        private SpriteRenderer[] spriteRenderers;
        private Material[] originalSharedMaterials;
        private MaterialPropertyBlock propertyBlock;

        public bool IsPickedUp => isPickedUp;
        public bool IsAvailable => enabled && !isPickedUp && gameObject.activeInHierarchy;

        protected virtual void Awake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalSharedMaterials = new Material[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
                originalSharedMaterials[i] = spriteRenderers[i]?.sharedMaterial;

            propertyBlock = new MaterialPropertyBlock();

            if (highlightMaterial == null)
                highlightMaterial = Resources.Load<Material>(HighlightMaterialResourcePath);

            Trace(
                $"Awake pickable={Describe(this)} dropPrefab={Describe(dropPrefab)} iconOverride={Describe(iconOverride)} highlightMaterial={Describe(highlightMaterial)} spriteRendererCount={spriteRenderers.Length}.");
        }

        private void OnDisable() => SetHighlight(false);

        public void SetInInteractRange(bool inRange)
        {
            Trace($"SetInInteractRange inRange={inRange} isAvailable={IsAvailable}.");
            SetHighlight(inRange && IsAvailable);
        }

        public void SetHighlight(bool on)
        {
            if (isHighlighted == on) return;
            isHighlighted = on;
            Trace($"SetHighlight on={on} highlightMaterial={Describe(highlightMaterial)}.");

            if (spriteRenderers == null) return;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (sr == null) continue;

                if (on && highlightMaterial != null)
                    sr.sharedMaterial = highlightMaterial;
                else if (originalSharedMaterials != null && i < originalSharedMaterials.Length)
                    sr.sharedMaterial = originalSharedMaterials[i];

                sr.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(HighlightId, on ? 1f : 0f);
                sr.SetPropertyBlock(propertyBlock);
            }
        }

        /// <summary>玩家尝试拾取。成功返回 true。</summary>
        public bool TryPickup(PlayerInteractor interactor, PlayerHeldItem held)
        {
            Trace(
                $"TryPickup begin pickable={Describe(this)} isAvailable={IsAvailable} isPickedUp={isPickedUp} active={gameObject.activeInHierarchy} interactor={Describe(interactor)} held={Describe(held)} heldHasItem={(held != null ? held.HasItem.ToString() : "null")}.");

            if (!IsAvailable)
            {
                Trace("TryPickup rejected: pickable is not available.");
                return false;
            }

            if (interactor == null)
            {
                Trace("TryPickup rejected: interactor is null.");
                return false;
            }

            if (held == null)
            {
                Trace("TryPickup rejected: held item component is null.");
                return false;
            }

            if (held.HasItem)
            {
                Trace($"TryPickup rejected: held item already has an item held={Describe(held)}.");
                return false;
            }

            if (!CanBePickedUp(interactor))
            {
                Trace($"TryPickup rejected: CanBePickedUp returned false for interactor={Describe(interactor)}.");
                return false;
            }

            GameObject prefab = ResolveDropPrefab();
            Trace($"TryPickup resolved prefab={Describe(prefab)} icon={Describe(ResolveIcon())} displayName='{ResolveDisplayName()}'.");
            if (prefab == null)
            {
                Debug.LogError($"[PickableObject] '{name}' 未指定 dropPrefab，无法拾取。", this);
                return false;
            }

            if (!held.TryHold(prefab, ResolveIcon(), ResolveDisplayName()))
            {
                Trace($"TryPickup rejected: held.TryHold returned false held={Describe(held)} prefab={Describe(prefab)}.");
                return false;
            }

            isPickedUp = true;
            SetHighlight(false);
            OnPickedUp(interactor);
            Trace($"TryPickup success pickable={Describe(this)} destroy original held={Describe(held)} heldHasItem={held.HasItem}.");
            Destroy(gameObject);
            return true;
        }

        protected virtual bool CanBePickedUp(PlayerInteractor interactor) => true;

        protected virtual void OnPickedUp(PlayerInteractor interactor) { }

        private GameObject ResolveDropPrefab()
        {
            if (dropPrefab != null) return dropPrefab;

#if UNITY_EDITOR
            GameObject source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (source != null) return source;
#endif
            return null;
        }

        private Sprite ResolveIcon()
        {
            if (iconOverride != null) return iconOverride;
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.sprite : null;
        }

        private string ResolveDisplayName()
            => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();

        private void Trace(string message)
        {
            if (traceLogging)
                Debug.Log($"[PickableObject] {message}", this);
        }

        private static string Describe(Object value)
        {
            return value != null ? $"{value.name}#{value.GetInstanceID()}" : "null";
        }
    }
}

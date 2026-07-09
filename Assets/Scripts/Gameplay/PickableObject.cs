using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 可拾取物体基类。挂在 prefab 上，需带 Trigger Collider2D。
    /// 玩家进入范围后按 E，由 <see cref="PlayerInteractor"/> 调用 <see cref="TryPickup"/>。
    /// 进入互动范围时切换到 SpriteHighlight 材质并开启高亮。
    /// </summary>
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

        private bool isPickedUp;
        private bool isHighlighted;
        private Collider2D triggerCollider;
        private SpriteRenderer[] spriteRenderers;
        private Material[] originalSharedMaterials;
        private MaterialPropertyBlock propertyBlock;

        public bool IsPickedUp => isPickedUp;
        public bool IsAvailable => enabled && !isPickedUp && gameObject.activeInHierarchy;
        public bool IsHighlighted => isHighlighted;

        protected virtual void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                Debug.LogWarning($"[PickableObject] '{name}' 的 Collider2D 建议设为 Trigger，以便检测玩家靠近。", this);
            }

            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalSharedMaterials = new Material[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
                originalSharedMaterials[i] = spriteRenderers[i] != null ? spriteRenderers[i].sharedMaterial : null;

            propertyBlock = new MaterialPropertyBlock();

            if (highlightMaterial == null)
                highlightMaterial = Resources.Load<Material>(HighlightMaterialResourcePath);

            if (highlightMaterial == null)
            {
                Debug.LogWarning(
                    $"[PickableObject] 未找到高亮材质 Resources/{HighlightMaterialResourcePath}，高亮将不可用。",
                    this);
            }
        }

        private void OnDisable()
        {
            SetHighlight(false);
        }

        /// <summary>进入/离开互动范围时由 PlayerInteractor 调用。</summary>
        public void SetInInteractRange(bool inRange)
        {
            SetHighlight(inRange && IsAvailable);
        }

        public void SetHighlight(bool enabled)
        {
            if (isHighlighted == enabled)
                return;

            isHighlighted = enabled;

            if (enabled)
                ApplyHighlightOn();
            else
                ApplyHighlightOff();
        }

        /// <summary>玩家尝试拾取。成功返回 true。</summary>
        public bool TryPickup(PlayerInteractor interactor)
        {
            if (!IsAvailable || interactor == null)
                return false;

            if (!CanBePickedUp(interactor))
                return false;

            PlayerHeldItem held = interactor.GetComponent<PlayerHeldItem>()
                ?? interactor.GetComponentInParent<PlayerHeldItem>();
            if (held == null)
            {
                Debug.LogWarning($"[PickableObject] 玩家缺少 PlayerHeldItem，无法拾取 {name}", this);
                return false;
            }

            if (held.HasItem)
                return false;

            GameObject prefab = ResolveDropPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[PickableObject] '{name}' 未指定 dropPrefab，无法拾取。", this);
                return false;
            }

            HeldItemData data = new(ResolveIcon(), prefab, ResolveDisplayName());
            if (!held.TryHold(data))
                return false;

            isPickedUp = true;
            SetHighlight(false);
            OnPickedUp(interactor);
            Destroy(gameObject);
            return true;
        }

        /// <summary>子类可覆盖：额外拾取条件。</summary>
        protected virtual bool CanBePickedUp(PlayerInteractor interactor) => true;

        /// <summary>子类可覆盖：拾取成功时的逻辑（加分、音效等）。</summary>
        protected virtual void OnPickedUp(PlayerInteractor interactor)
        {
            Debug.Log($"[PickableObject] 拾取 {name}", this);
        }

        private GameObject ResolveDropPrefab()
        {
            if (dropPrefab != null)
                return dropPrefab;

#if UNITY_EDITOR
            // 编辑器下：若场景实例来自 prefab，用对应 prefab 资源
            GameObject source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (source != null)
                return source;
#endif
            return null;
        }

        private Sprite ResolveIcon()
        {
            if (iconOverride != null)
                return iconOverride;

            if (spriteRenderers != null)
            {
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i] != null && spriteRenderers[i].sprite != null)
                        return spriteRenderers[i].sprite;
                }
            }

            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.sprite : null;
        }

        private string ResolveDisplayName()
        {
            return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        }

        private void ApplyHighlightOn()
        {
            if (spriteRenderers == null || highlightMaterial == null || propertyBlock == null)
                return;

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (sr == null)
                    continue;

                sr.sharedMaterial = highlightMaterial;
                sr.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(HighlightId, 1f);
                sr.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyHighlightOff()
        {
            if (spriteRenderers == null)
                return;

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (sr == null)
                    continue;

                if (originalSharedMaterials != null && i < originalSharedMaterials.Length)
                    sr.sharedMaterial = originalSharedMaterials[i];

                if (propertyBlock != null)
                {
                    sr.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetFloat(HighlightId, 0f);
                    sr.SetPropertyBlock(propertyBlock);
                }
            }
        }
    }
}

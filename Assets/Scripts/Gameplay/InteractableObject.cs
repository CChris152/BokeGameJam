using UnityEngine;

namespace BokeGameJam.Gameplay
{
    public enum InteractMode
    {
        PickUp,
        Trigger
    }

    /// <summary>
    /// 可交互物体基类。默认可捡起；子类可改为 Trigger 模式。
    /// mechanismId 用于把同一机制下的 A/B/C 绑在一起，避免多机制交叉。
    /// 所属层级由 <see cref="LevelObject.LevelLayer"/> 决定（Shared / A / B）。
    /// Prefab 结构：根节点挂交互脚本；子物体 Image 挂 SpriteRenderer + Collider2D。
    /// 进入交互范围且 <see cref="ShouldShowInteractHint"/> 为真时显示 InteractHint。
    /// </summary>
    public class InteractableObject : LevelObject, IInteractable
    {
        private const string ImageChildName = "Image";
        private const string InteractHintChildName = "InteractHint";
        private const string DefaultHintPrefabResourcePath = "Prefabs/Terrians/Interactable/InteractHint";

        [SerializeField] private string mechanismId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite iconOverride;

        [Header("Interact Hint")]
        [Tooltip("互动提示预制体；留空则尝试 Resources 路径。")]
        [SerializeField] private GameObject interactHintPrefab;
        [Tooltip("相对本物体的本地偏移（下方为正 Y 负值）。")]
        [SerializeField] private Vector2 hintLocalOffset = new(0f, -0.85f);
        [Tooltip("互动提示本地缩放。")]
        [SerializeField] private Vector3 hintLocalScale = Vector3.one;
        [SerializeField] private string hintPrefabResourcePath = DefaultHintPrefabResourcePath;

        private Collider2D col;
        private SpriteRenderer spriteRenderer;
        private Vector3 originalLocalScale;
        private bool isHeld;
        private bool isInInteractRange;
        private GameObject hintInstance;

        public bool IsHeld => isHeld;
        public virtual InteractMode Mode => InteractMode.PickUp;
        public Vector3 InteractionPosition => transform.position;
        public string MechanismId => mechanismId != null ? mechanismId.Trim() : string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => iconOverride != null
            ? iconOverride
            : spriteRenderer != null ? spriteRenderer.sprite : null;

        protected Collider2D Col => col;
        protected SpriteRenderer SpriteRenderer => spriteRenderer;
        protected bool IsInInteractRange => isInInteractRange;

        /// <summary>关卡编辑器写入配置；子类可覆盖以处理序列字段。</summary>
        public virtual void ApplyEditorConfig(string newMechanismId, string sequenceGroupId = null, int sequenceIndex = 0)
        {
            mechanismId = newMechanismId != null ? newMechanismId.Trim() : string.Empty;
        }

        protected virtual void Awake()
        {
            ResolveVisualAndCollider();
            originalLocalScale = transform.localScale;
        }

        protected virtual void OnDisable()
        {
            isInInteractRange = false;
            SetHintVisible(false);
        }

        /// <summary>
        /// 优先从子物体 Image 取 SpriteRenderer / Collider2D；
        /// 兼容旧 prefab（组件仍在根节点上）。
        /// </summary>
        protected void ResolveVisualAndCollider()
        {
            Transform image = transform.Find(ImageChildName);
            if (image != null)
            {
                col = image.GetComponent<Collider2D>();
                spriteRenderer = image.GetComponent<SpriteRenderer>();
            }

            if (col == null)
                col = GetComponent<Collider2D>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (col == null)
                col = GetComponentInChildren<Collider2D>(true);
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        public virtual bool CanInteract(PlayerInteractor interactor)
        {
            // 切世界后非当前层物体可能仍留在 PlayerInteractor.nearby，必须拒绝未激活对象。
            return !isHeld
                && isActiveAndEnabled
                && gameObject.activeInHierarchy;
        }

        public virtual void SetInInteractRange(bool inRange)
        {
            isInInteractRange = inRange;
            RefreshInteractHint();
        }

        public virtual bool Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return false;

            if (Mode == InteractMode.Trigger)
            {
                OnInteract(interactor);
                return true;
            }

            return interactor != null && interactor.TryPickUp(this);
        }

        /// <summary>Trigger 模式由子类实现；PickUp 模式由 PlayerInteractor 处理。</summary>
        public virtual void OnInteract(PlayerInteractor interactor)
        {
        }

        public bool MatchesMechanism(InteractableObject other)
        {
            if (other == null)
                return false;

            string selfId = MechanismId;
            string otherId = other.MechanismId;
            if (string.IsNullOrEmpty(selfId) || string.IsNullOrEmpty(otherId))
                return false;

            return string.Equals(selfId, otherId, System.StringComparison.Ordinal);
        }

        public virtual void PickUp()
        {
            isHeld = true;
            isInInteractRange = false;
            RefreshInteractHint();
            gameObject.SetActive(false);
        }

        public virtual void Drop(Vector2 worldPosition)
        {
            isHeld = false;
            transform.position = worldPosition;
            transform.localScale = originalLocalScale;
            gameObject.SetActive(true);

            if (col != null)
                col.enabled = true;

            RefreshInteractHint();
        }

        /// <summary>是否显示互动提示；子类可叠加解锁 / 持有物等条件。</summary>
        protected virtual bool ShouldShowInteractHint()
        {
            return isInInteractRange
                && !isHeld
                && isActiveAndEnabled
                && gameObject.activeInHierarchy;
        }

        /// <summary>按 <see cref="ShouldShowInteractHint"/> 刷新提示显隐。</summary>
        protected void RefreshInteractHint()
        {
            SetHintVisible(ShouldShowInteractHint());
        }

        private void SetHintVisible(bool show)
        {
            if (!show)
            {
                // Instantiate 复制出的物体可能带着已激活的 InteractHint，但 hintInstance 字段为空。
                if (hintInstance == null)
                {
                    Transform existing = transform.Find(InteractHintChildName);
                    if (existing != null)
                        hintInstance = existing.gameObject;
                }

                if (hintInstance != null)
                    hintInstance.SetActive(false);
                return;
            }

            EnsureHintInstance();
            if (hintInstance == null)
                return;

            hintInstance.transform.localPosition = hintLocalOffset;
            hintInstance.transform.localScale = hintLocalScale;
            hintInstance.SetActive(true);
        }

        private void EnsureHintInstance()
        {
            if (hintInstance != null)
                return;

            Transform existing = transform.Find(InteractHintChildName);
            if (existing != null)
            {
                hintInstance = existing.gameObject;
                hintInstance.transform.localScale = hintLocalScale;
                return;
            }

            GameObject prefab = interactHintPrefab;
            if (prefab == null && !string.IsNullOrWhiteSpace(hintPrefabResourcePath))
                prefab = Resources.Load<GameObject>(hintPrefabResourcePath.Trim());

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[InteractableObject] '{name}' 缺少互动提示预制体（interactHintPrefab / Resources '{hintPrefabResourcePath}'）。",
                    this);
                return;
            }

            hintInstance = Instantiate(prefab, transform);
            hintInstance.name = InteractHintChildName;
            hintInstance.transform.localPosition = hintLocalOffset;
            hintInstance.transform.localRotation = Quaternion.identity;
            hintInstance.transform.localScale = hintLocalScale;
            hintInstance.SetActive(false);
        }
    }
}

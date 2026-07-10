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
    /// </summary>
    public class InteractableObject : LevelObject, IInteractable
    {
        private const string ImageChildName = "Image";

        [SerializeField] private string mechanismId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite iconOverride;
        [SerializeField] private Vector2 holdLocalOffset = new(0.35f, 0.15f);

        private Collider2D col;
        private SpriteRenderer spriteRenderer;
        private Vector3 originalLocalScale;
        private bool isHeld;

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
            return !isHeld;
        }

        public virtual void SetInInteractRange(bool inRange)
        {
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

        public virtual void PickUp(Transform holder)
        {
            isHeld = true;
            transform.SetParent(holder, false);
            transform.localPosition = holdLocalOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = originalLocalScale;

            if (col != null)
                col.enabled = false;
        }

        public virtual void Drop(Vector2 worldPosition)
        {
            isHeld = false;
            transform.SetParent(null, true);
            transform.position = worldPosition;
            transform.localScale = originalLocalScale;

            if (col != null)
                col.enabled = true;
        }
    }
}

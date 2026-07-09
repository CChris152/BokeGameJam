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
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InteractableObject : MonoBehaviour
    {
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
        public string MechanismId => mechanismId != null ? mechanismId.Trim() : string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => iconOverride != null
            ? iconOverride
            : spriteRenderer != null ? spriteRenderer.sprite : null;

        protected Collider2D Col => col;
        protected SpriteRenderer SpriteRenderer => spriteRenderer;

        protected virtual void Awake()
        {
            col = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalLocalScale = transform.localScale;
        }

        public virtual bool CanInteract(PlayerInteractor interactor)
        {
            return !isHeld;
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

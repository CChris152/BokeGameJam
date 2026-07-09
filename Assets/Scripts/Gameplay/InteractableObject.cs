using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 可交互物体基类：由 <see cref="PlayerInteractor"/> 在范围内按 E 捡起 / 持有时按 E 丢弃。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InteractableObject : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite iconOverride;
        [SerializeField] private Vector2 holdLocalOffset = new(0.35f, 0.15f);

        private Collider2D col;
        private SpriteRenderer spriteRenderer;
        private Vector3 originalLocalScale;
        private bool isHeld;

        public bool IsHeld => isHeld;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => iconOverride != null
            ? iconOverride
            : spriteRenderer != null ? spriteRenderer.sprite : null;

        protected virtual void Awake()
        {
            col = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalLocalScale = transform.localScale;
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
            // Player scale is 0.5; SetParent(worldPositionStays) would bake that into localScale.
            transform.localScale = originalLocalScale;

            if (col != null)
                col.enabled = true;
        }
    }
}

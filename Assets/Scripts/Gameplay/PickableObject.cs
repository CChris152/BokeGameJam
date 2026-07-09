using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 可拾取物体基类。挂在 prefab 上，需带 Trigger Collider2D。
    /// 玩家进入范围后按 E，由 <see cref="PlayerInteractor"/> 调用 <see cref="TryPickup"/>。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PickableObject : MonoBehaviour
    {
        [Header("Pickup")]
        [Tooltip("拾取后销毁场景实例")]
        [SerializeField] private bool destroyOnPickup = true;

        [Tooltip("是否可被重复拾取（destroyOnPickup 为 false 时有意义）")]
        [SerializeField] private bool canPickupMultipleTimes;

        private bool isPickedUp;
        private Collider2D triggerCollider;

        public bool IsPickedUp => isPickedUp;
        public bool IsAvailable => enabled && !isPickedUp && gameObject.activeInHierarchy;

        protected virtual void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                Debug.LogWarning($"[PickableObject] '{name}' 的 Collider2D 建议设为 Trigger，以便检测玩家靠近。", this);
            }
        }

        /// <summary>玩家尝试拾取。成功返回 true。</summary>
        public bool TryPickup(PlayerInteractor interactor)
        {
            if (!IsAvailable || interactor == null)
                return false;

            if (!CanBePickedUp(interactor))
                return false;

            isPickedUp = true;
            OnPickedUp(interactor);

            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
            else if (!canPickupMultipleTimes)
            {
                if (triggerCollider != null)
                    triggerCollider.enabled = false;
                enabled = false;
            }
            else
            {
                isPickedUp = false;
            }

            return true;
        }

        /// <summary>子类可覆盖：额外拾取条件。</summary>
        protected virtual bool CanBePickedUp(PlayerInteractor interactor) => true;

        /// <summary>子类可覆盖：拾取成功时的逻辑（加分、音效等）。</summary>
        protected virtual void OnPickedUp(PlayerInteractor interactor)
        {
            Debug.Log($"[PickableObject] 拾取 {name}", this);
        }
    }
}

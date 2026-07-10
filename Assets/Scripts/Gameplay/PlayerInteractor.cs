using System.Collections.Generic;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Input;
using BokeGameJam.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 玩家交互：范围内按 E 对最近可交互物执行捡起或触发。
    /// 持有物品时优先交付 C 或触发非拾取谜题，否则丢弃。
    /// 切换到里世界（B）时，手中的花会自动丢弃。
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Vector2 dropOffset = new(0.6f, 0f);
        [SerializeField] private bool faceAwareDrop = true;

        private readonly HashSet<IInteractable> nearby = new();
        private readonly Dictionary<IInteractable, int> contactCounts = new();
        private readonly List<IInteractable> removeBuffer = new();
        private InteractableObject held;

        public bool HasHeldItem => held != null;
        public InteractableObject HeldItem => held;

        private void OnEnable()
        {
            EventManager.On(InputEvents.PlayerInteractPressed, OnInteractPressed);
            EventManager.On<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.PlayerInteractPressed, OnInteractPressed);
            EventManager.Off<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);

            foreach (IInteractable interactable in nearby)
            {
                if (IsUnityObjectAlive(interactable))
                    interactable.SetInInteractRange(false);
            }

            nearby.Clear();
            contactCounts.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            IInteractable interactable = FindInteractable(other);
            // 用 trackable 而非 CanInteract：busy/冷却中进入范围也应登记，结束后可直接再按。
            if (!IsTrackableInteractable(interactable))
                return;

            if (contactCounts.TryGetValue(interactable, out int count))
            {
                contactCounts[interactable] = count + 1;
                return;
            }

            contactCounts[interactable] = 1;
            if (nearby.Add(interactable))
                interactable.SetInInteractRange(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            IInteractable interactable = FindInteractable(other);
            if (interactable == null)
                return;

            if (!contactCounts.TryGetValue(interactable, out int count))
                return;

            if (count > 1)
            {
                contactCounts[interactable] = count - 1;
                return;
            }

            contactCounts.Remove(interactable);
            if (nearby.Remove(interactable))
                interactable.SetInInteractRange(false);
        }

        private void OnInteractPressed()
        {
            // 对话框 / 选项面板打开或刚关闭：避免同帧误交互
            if (DialoguePopup.BlocksInteract)
                return;

            // 鬼魂 D：空手或持物都可对话（优先于丢弃）
            InteractableObjectD ghost = FindNearestGhost();
            if (ghost != null)
            {
                TryInteractWith(ghost);
                return;
            }

            if (held != null)
            {
                InteractableObjectC delivery = FindNearestDelivery();
                if (delivery != null)
                {
                    TryInteractWith(delivery);
                    return;
                }

                IInteractable target = FindNearestTarget(allowPickups: false);
                if (TryInteractWith(target))
                    return;

                DropHeld();
                return;
            }

            TryInteractWith(FindNearestTarget());
        }

        public bool TryPickUp(InteractableObject item)
        {
            if (item == null || held != null || item.Mode != InteractMode.PickUp || !item.CanInteract(this))
                return false;

            nearby.Remove(item);
            contactCounts.Remove(item);
            held = item;
            item.SetInInteractRange(false);
            item.PickUp();
            EmitHeldChanged();
            return true;
        }

        private void DropHeld()
        {
            InteractableObject item = held;
            held = null;

            Vector2 dropPos = (Vector2)transform.position + GetDropOffset();
            item.Drop(dropPos);
            EmitHeldChanged();
        }

        /// <summary>切换世界时清理失效 nearby；切到里世界（B）时自动丢弃手中的花。</summary>
        private void OnActiveWorldChanged(WorldId world)
        {
            PruneInactiveNearby();

            if (world != WorldId.B || held is not InteractableObjectFlower)
                return;

            DropHeld();
        }

        /// <summary>
        /// 切世界时 SetActive(false) 往往不会触发 OnTriggerExit，
        /// 表世界物体会残留在 nearby，导致里世界误交互。
        /// </summary>
        private void PruneInactiveNearby()
        {
            removeBuffer.Clear();

            foreach (IInteractable interactable in nearby)
            {
                if (!IsTrackableInteractable(interactable))
                    removeBuffer.Add(interactable);
            }

            for (int i = 0; i < removeBuffer.Count; i++)
            {
                IInteractable interactable = removeBuffer[i];
                if (IsUnityObjectAlive(interactable))
                    interactable.SetInInteractRange(false);
                nearby.Remove(interactable);
                contactCounts.Remove(interactable);
            }
        }

        /// <summary>交付处消耗当前持有物（销毁）。</summary>
        public void ConsumeHeldItem()
        {
            if (held == null)
                return;

            InteractableObject item = ReleaseHeldItem();
            Destroy(item.gameObject);
        }

        /// <summary>
        /// Removes the current item from the hand without destroying or dropping it.
        /// Delivery systems can then hide it, return it to an origin, or animate it.
        /// </summary>
        public InteractableObject ReleaseHeldItem()
        {
            if (held == null)
                return null;

            InteractableObject item = held;
            held = null;
            nearby.Remove(item);
            contactCounts.Remove(item);
            EmitHeldChanged();
            return item;
        }

        private IInteractable FindNearestTarget(bool allowPickups = true)
        {
            removeBuffer.Clear();
            IInteractable best = null;
            float bestDist = float.MaxValue;
            Vector2 origin = transform.position;

            foreach (IInteractable interactable in nearby)
            {
                if (!IsTrackableInteractable(interactable))
                {
                    if (IsUnityObjectAlive(interactable))
                        interactable.SetInInteractRange(false);
                    removeBuffer.Add(interactable);
                    continue;
                }

                // busy/冷却等临时不可互动：保留在 nearby，只是本帧不选中。
                if (!interactable.CanInteract(this))
                    continue;

                if (!allowPickups && interactable is InteractableObject { Mode: InteractMode.PickUp })
                    continue;

                float dist = ((Vector2)interactable.InteractionPosition - origin).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = interactable;
                }
            }

            for (int i = 0; i < removeBuffer.Count; i++)
            {
                nearby.Remove(removeBuffer[i]);
                contactCounts.Remove(removeBuffer[i]);
            }

            return best;
        }

        private InteractableObjectC FindNearestDelivery()
        {
            removeBuffer.Clear();
            InteractableObjectC best = null;
            float bestDist = float.MaxValue;
            Vector2 origin = transform.position;

            foreach (IInteractable interactable in nearby)
            {
                if (!IsUnityObjectAlive(interactable))
                {
                    removeBuffer.Add(interactable);
                    continue;
                }

                if (interactable is not InteractableObjectC delivery)
                    continue;

                if (!delivery.CanInteract(this))
                    continue;

                float dist = ((Vector2)delivery.InteractionPosition - origin).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = delivery;
                }
            }

            for (int i = 0; i < removeBuffer.Count; i++)
            {
                nearby.Remove(removeBuffer[i]);
                contactCounts.Remove(removeBuffer[i]);
            }

            return best;
        }

        private InteractableObjectD FindNearestGhost()
        {
            InteractableObjectD best = null;
            float bestDist = float.MaxValue;
            Vector2 origin = transform.position;

            nearby.RemoveWhere(item => item == null);

            foreach (IInteractable item in nearby)
            {
                if (item is not InteractableObjectD ghost)
                    continue;

                if (!ghost.CanInteract(this))
                    continue;

                float dist = ((Vector2)ghost.transform.position - origin).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = ghost;
                }
            }

            return best;
        }

        private bool TryInteractWith(IInteractable interactable)
        {
            if (!IsValidInteractable(interactable))
                return false;

            bool interacted = interactable.Interact(this);
            if (!interacted)
                return false;

            bool hasDedicatedInteractionAudio = interactable is InteractableObjectLightSwitch
                || interactable is InteractableObjectStreetLamp;
            if (!hasDedicatedInteractionAudio && GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.InteractionConfirm);

            // 仅在对象销毁/失活时移出 nearby；busy 等临时不可互动仍保留，冷却后可再按。
            if (!IsTrackableInteractable(interactable))
            {
                if (IsUnityObjectAlive(interactable))
                    interactable.SetInInteractRange(false);
                nearby.Remove(interactable);
                contactCounts.Remove(interactable);
            }

            return true;
        }

        private static IInteractable FindInteractable(Collider2D other)
        {
            if (other == null)
                return null;

            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
            IInteractable fallback = null;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IInteractable interactable)
                    continue;

                if (interactable is not InteractableObject)
                    return interactable;

                fallback ??= interactable;
            }

            return fallback;
        }

        /// <summary>
        /// 仍在场景中、可继续跟踪重叠的交互物（含 busy/冷却中）。
        /// 切世界失活、销毁的对象不算。
        /// </summary>
        private static bool IsTrackableInteractable(IInteractable interactable)
        {
            if (!IsUnityObjectAlive(interactable))
                return false;

            // 非当前世界层被 SetActive(false) 后仍可能留在 nearby。
            if (interactable is Behaviour behaviour
                && (!behaviour.isActiveAndEnabled || !behaviour.gameObject.activeInHierarchy))
                return false;

            return true;
        }

        /// <summary>当前帧可以真正执行交互（trackable 且 CanInteract）。</summary>
        private bool IsValidInteractable(IInteractable interactable)
        {
            return IsTrackableInteractable(interactable) && interactable.CanInteract(this);
        }

        private static bool IsUnityObjectAlive(IInteractable interactable)
        {
            if (interactable == null)
                return false;

            return interactable is not Object unityObject || unityObject != null;
        }

        private Vector2 GetDropOffset()
        {
            if (!faceAwareDrop)
                return dropOffset;

            float facing = -Mathf.Sign(transform.localScale.x);
            if (Mathf.Approximately(facing, 0f))
                facing = -1f;

            return new Vector2(Mathf.Abs(dropOffset.x) * facing, dropOffset.y);
        }

        private void EmitHeldChanged()
        {
            if (held == null)
            {
                EventManager.Emit(GameEvents.HeldItemChanged, HeldItemInfo.Empty);
                return;
            }

            EventManager.Emit(
                GameEvents.HeldItemChanged,
                new HeldItemInfo(true, held.Icon, held.DisplayName, held.MechanismId));
        }
    }
}

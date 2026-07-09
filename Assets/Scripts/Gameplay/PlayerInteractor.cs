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
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Vector2 dropOffset = new(0.6f, 0f);
        [SerializeField] private bool faceAwareDrop = true;

        private readonly HashSet<IInteractable> nearby = new();
        private readonly List<IInteractable> removeBuffer = new();
        private InteractableObject held;

        public bool HasHeldItem => held != null;
        public InteractableObject HeldItem => held;

        private void OnEnable()
        {
            EventManager.On(InputEvents.PlayerInteractPressed, OnInteractPressed);
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.PlayerInteractPressed, OnInteractPressed);

            foreach (IInteractable interactable in nearby)
            {
                if (IsUnityObjectAlive(interactable))
                    interactable.SetInInteractRange(false);
            }

            nearby.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            IInteractable interactable = FindInteractable(other);
            if (!IsValidInteractable(interactable))
                return;

            if (nearby.Add(interactable))
                interactable.SetInInteractRange(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            IInteractable interactable = FindInteractable(other);
            if (interactable == null)
                return;

            if (nearby.Remove(interactable))
                interactable.SetInInteractRange(false);
        }

        private void OnInteractPressed()
        {
            // 对话框打开/刚关闭：由 DialoguePopup 处理关闭，避免同帧误交互
            if (DialoguePopup.BlocksInteract)
                return;

            // 鬼魂 D：空手或持物都可对话（优先于丢弃）
            InteractableObjectD ghost = FindNearestGhost();
            if (ghost != null)
            {
                ghost.OnInteract(this);
                return;
            }

            if (held != null)
            {
                InteractableObjectC delivery = FindNearestDelivery();
                if (delivery != null)
                {
                    delivery.OnInteract(this);
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
            held = item;
            item.SetInInteractRange(false);
            item.PickUp(transform);
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

        /// <summary>交付处消耗当前持有物（销毁）。</summary>
        public void ConsumeHeldItem()
        {
            if (held == null)
                return;

            InteractableObject item = held;
            held = null;
            nearby.Remove(item);
            Destroy(item.gameObject);
            EmitHeldChanged();
        }

        private IInteractable FindNearestTarget(bool allowPickups = true)
        {
            removeBuffer.Clear();
            IInteractable best = null;
            float bestDist = float.MaxValue;
            Vector2 origin = transform.position;

            foreach (IInteractable interactable in nearby)
            {
                if (!IsValidInteractable(interactable))
                {
                    if (IsUnityObjectAlive(interactable))
                        interactable.SetInInteractRange(false);
                    removeBuffer.Add(interactable);
                    continue;
                }

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
                nearby.Remove(removeBuffer[i]);

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
                nearby.Remove(removeBuffer[i]);

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

            if (!IsValidInteractable(interactable))
            {
                if (IsUnityObjectAlive(interactable))
                    interactable.SetInInteractRange(false);
                nearby.Remove(interactable);
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

        private bool IsValidInteractable(IInteractable interactable)
        {
            return IsUnityObjectAlive(interactable) && interactable.CanInteract(this);
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

            float facing = Mathf.Sign(transform.localScale.x);
            if (Mathf.Approximately(facing, 0f))
                facing = 1f;

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

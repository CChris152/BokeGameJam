using System.Collections.Generic;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Input;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 玩家交互：范围内按 E 对最近可交互物执行捡起或触发；
    /// 持有物品时优先对附近交付处 C 交付，否则丢弃。
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Vector2 dropOffset = new(0.6f, 0f);
        [SerializeField] private bool faceAwareDrop = true;

        private readonly HashSet<InteractableObject> nearby = new();
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
            nearby.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            InteractableObject item = other.GetComponentInParent<InteractableObject>();
            if (item != null)
                nearby.Add(item);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            InteractableObject item = other.GetComponentInParent<InteractableObject>();
            if (item != null)
                nearby.Remove(item);
        }

        private void OnInteractPressed()
        {
            if (held != null)
            {
                InteractableObjectC delivery = FindNearestDelivery();
                if (delivery != null)
                {
                    delivery.OnInteract(this);
                    return;
                }

                DropHeld();
                return;
            }

            InteractableObject target = FindNearestNearby();
            if (target == null)
                return;

            if (target.Mode == InteractMode.Trigger)
            {
                target.OnInteract(this);
                return;
            }

            PickUp(target);
        }

        private void PickUp(InteractableObject item)
        {
            nearby.Remove(item);
            held = item;
            item.PickUp(transform);
            EmitHeldChanged();
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

        private InteractableObject FindNearestNearby()
        {
            InteractableObject best = null;
            float bestDist = float.MaxValue;
            Vector2 origin = transform.position;

            nearby.RemoveWhere(item => item == null || !item.CanInteract(this));

            foreach (InteractableObject item in nearby)
            {
                float dist = ((Vector2)item.transform.position - origin).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = item;
                }
            }

            return best;
        }

        private InteractableObjectC FindNearestDelivery()
        {
            InteractableObjectC best = null;
            float bestDist = float.MaxValue;
            Vector2 origin = transform.position;

            nearby.RemoveWhere(item => item == null);

            foreach (InteractableObject item in nearby)
            {
                if (item is not InteractableObjectC delivery)
                    continue;

                if (!delivery.CanInteract(this))
                    continue;

                float dist = ((Vector2)delivery.transform.position - origin).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = delivery;
                }
            }

            return best;
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

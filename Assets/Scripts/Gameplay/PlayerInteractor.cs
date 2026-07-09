using System.Collections.Generic;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Input;

namespace BokeGameJam.Gameplay
{
    /// <summary>玩家交互：E = 有持有物则放下，否则拾取范围内最近可拾取物。</summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Detect")]
        [Tooltip("若为空，用本物体上的 Collider2D 做触发检测")]
        [SerializeField] private Collider2D interactTrigger;

        [Tooltip("同范围内多个可拾取物时，优先距离最近")]
        [SerializeField] private bool preferNearest = true;

        [Header("Debug")]
        [SerializeField] private bool traceLogging = true;

        private readonly HashSet<PickableObject> inRange = new();
        private PlayerHeldItem heldItem;

        public PlayerHeldItem HeldItem => heldItem;

        private void Awake()
        {
            if (interactTrigger == null)
                interactTrigger = GetComponent<Collider2D>();

            heldItem = GetComponent<PlayerHeldItem>() ?? GetComponentInParent<PlayerHeldItem>();
            Trace($"Awake interactor={Describe(this)} trigger={Describe(interactTrigger)} heldItem={Describe(heldItem)}.");
        }

        private void OnEnable()
        {
            EventManager.On(InputEvents.PlayerInteractPressed, OnInteractPressed);
            Trace($"OnEnable subscribed to {InputEvents.PlayerInteractPressed}.");
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.PlayerInteractPressed, OnInteractPressed);
            Trace($"OnDisable unsubscribed from {InputEvents.PlayerInteractPressed}. Clearing {inRange.Count} targets.");

            foreach (PickableObject p in inRange)
                if (p != null) p.SetInInteractRange(false);
            inRange.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PickableObject pickable = other.GetComponentInParent<PickableObject>();
            if (pickable == null)
            {
                Trace($"TriggerEnter ignored: collider={Describe(other)} has no PickableObject parent.");
                return;
            }

            if (!pickable.IsAvailable)
            {
                Trace($"TriggerEnter ignored: pickable={Describe(pickable)} is not available.");
                return;
            }

            if (inRange.Add(pickable))
            {
                Trace($"TriggerEnter added pickable={Describe(pickable)} inRangeCount={inRange.Count}.");
                pickable.SetInInteractRange(true);
            }
            else
            {
                Trace($"TriggerEnter skipped duplicate pickable={Describe(pickable)} inRangeCount={inRange.Count}.");
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PickableObject pickable = other.GetComponentInParent<PickableObject>();
            if (pickable == null)
            {
                Trace($"TriggerExit ignored: collider={Describe(other)} has no PickableObject parent.");
                return;
            }

            if (inRange.Remove(pickable))
            {
                Trace($"TriggerExit removed pickable={Describe(pickable)} inRangeCount={inRange.Count}.");
                pickable.SetInInteractRange(false);
            }
            else
            {
                Trace($"TriggerExit did not contain pickable={Describe(pickable)} inRangeCount={inRange.Count}.");
            }
        }

        private void OnInteractPressed()
        {
            if (!isActiveAndEnabled) return;

            Trace(
                $"Interact pressed interactor={Describe(this)} heldItem={Describe(heldItem)} heldHasItem={(heldItem != null ? heldItem.HasItem.ToString() : "null")} inRangeCount={inRange.Count}.");

            // 手上有物：放下
            if (heldItem != null && heldItem.HasItem)
            {
                bool dropped = heldItem.TryDrop();
                Trace($"Interact drop result={dropped} heldItem={Describe(heldItem)} heldHasItem={heldItem.HasItem}.");
                return;
            }

            // 手上空：拾取
            PickableObject target = FindBestTarget();
            if (target == null)
            {
                Trace("Interact pickup skipped: no target.");
                return;
            }

            bool pickedUp = target.TryPickup(this, heldItem);
            Trace($"Interact pickup target={Describe(target)} result={pickedUp} heldItem={Describe(heldItem)} heldHasItem={(heldItem != null ? heldItem.HasItem.ToString() : "null")}.");
            if (pickedUp)
            {
                inRange.Remove(target);
                Trace($"Interact removed picked target from range. inRangeCount={inRange.Count}.");
            }
        }

        private PickableObject FindBestTarget()
        {
            PickableObject best = null;
            float bestDistSq = float.PositiveInfinity;

            int removed = inRange.RemoveWhere(p => p == null || !p.IsAvailable);
            if (removed > 0)
                Trace($"FindBestTarget pruned unavailable targets count={removed} remaining={inRange.Count}.");

            foreach (PickableObject p in inRange)
            {
                if (!preferNearest) { best = p; break; }

                float distSq = (p.transform.position - transform.position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = p;
                }
            }
            Trace($"FindBestTarget result={Describe(best)} preferNearest={preferNearest} inRangeCount={inRange.Count}.");
            return best;
        }

        private void Trace(string message)
        {
            if (traceLogging)
                Debug.Log($"[PlayerInteractor] {message}", this);
        }

        private static string Describe(Object value)
        {
            return value != null ? $"{value.name}#{value.GetInstanceID()}" : "null";
        }
    }
}

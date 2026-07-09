using System.Collections.Generic;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Input;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 玩家交互：E = 有持有物则放下，否则拾取范围内最近可拾取物。
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Detect")]
        [Tooltip("若为空，用本物体上的 Collider2D 做触发检测")]
        [SerializeField] private Collider2D interactTrigger;

        [Tooltip("同范围内多个可拾取物时，优先距离最近")]
        [SerializeField] private bool preferNearest = true;

        private readonly HashSet<PickableObject> inRange = new();
        private readonly List<PickableObject> removeBuffer = new();
        private PlayerHeldItem heldItem;

        public IReadOnlyCollection<PickableObject> InRange => inRange;

        private void Awake()
        {
            if (interactTrigger == null)
                interactTrigger = GetComponent<Collider2D>();

            heldItem = GetComponent<PlayerHeldItem>() ?? GetComponentInParent<PlayerHeldItem>();
            if (heldItem == null)
                heldItem = gameObject.AddComponent<PlayerHeldItem>();
        }

        private void OnEnable()
        {
            EventManager.On(InputEvents.PlayerInteractPressed, OnInteractPressed);
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.PlayerInteractPressed, OnInteractPressed);

            foreach (PickableObject pickable in inRange)
            {
                if (pickable != null)
                    pickable.SetInInteractRange(false);
            }

            inRange.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PickableObject pickable = other.GetComponent<PickableObject>()
                ?? other.GetComponentInParent<PickableObject>();
            if (pickable == null || !pickable.IsAvailable)
                return;

            if (inRange.Add(pickable))
                pickable.SetInInteractRange(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PickableObject pickable = other.GetComponent<PickableObject>()
                ?? other.GetComponentInParent<PickableObject>();
            if (pickable == null)
                return;

            if (inRange.Remove(pickable))
                pickable.SetInInteractRange(false);
        }

        private void OnInteractPressed()
        {
            if (!isActiveAndEnabled)
                return;

            // 手上有东西：E 放下
            if (heldItem != null && heldItem.HasItem)
            {
                heldItem.TryDrop();
                return;
            }

            // 手上空：尝试拾取
            PickableObject target = FindBestTarget();
            if (target == null)
                return;

            if (target.TryPickup(this))
            {
                target.SetInInteractRange(false);
                inRange.Remove(target);
            }
        }

        private PickableObject FindBestTarget()
        {
            removeBuffer.Clear();
            PickableObject best = null;
            float bestDistSq = float.PositiveInfinity;

            foreach (PickableObject pickable in inRange)
            {
                if (pickable == null || !pickable.IsAvailable)
                {
                    if (pickable != null)
                        pickable.SetInInteractRange(false);
                    removeBuffer.Add(pickable);
                    continue;
                }

                if (!preferNearest)
                {
                    best = pickable;
                    break;
                }

                float distSq = (pickable.transform.position - transform.position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = pickable;
                }
            }

            for (int i = 0; i < removeBuffer.Count; i++)
                inRange.Remove(removeBuffer[i]);

            return best;
        }
    }
}

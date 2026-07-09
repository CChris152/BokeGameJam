using System.Collections.Generic;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Input;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 玩家交互：跟踪范围内的 <see cref="PickableObject"/>，响应 E 键拾取最近的一个。
    /// 挂在 Player 上；可拾取物需带 Trigger Collider2D。
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

        public IReadOnlyCollection<PickableObject> InRange => inRange;

        private void Awake()
        {
            if (interactTrigger == null)
                interactTrigger = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            EventManager.On(InputEvents.PlayerInteractPressed, OnInteractPressed);
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.PlayerInteractPressed, OnInteractPressed);
            inRange.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PickableObject pickable = other.GetComponent<PickableObject>()
                ?? other.GetComponentInParent<PickableObject>();
            if (pickable != null && pickable.IsAvailable)
                inRange.Add(pickable);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PickableObject pickable = other.GetComponent<PickableObject>()
                ?? other.GetComponentInParent<PickableObject>();
            if (pickable != null)
                inRange.Remove(pickable);
        }

        private void OnInteractPressed()
        {
            if (!isActiveAndEnabled)
                return;

            PickableObject target = FindBestTarget();
            if (target == null)
                return;

            if (target.TryPickup(this))
                inRange.Remove(target);
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

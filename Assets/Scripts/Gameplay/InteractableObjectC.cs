using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 可交互物体 C：交付处。
    /// 仅在「持有同 mechanismId 的物品 A」或「同 mechanismId 的物品 B 已成功触发」时开放；
    /// 不同机制的交付处彼此隔离，不可交叉。
    /// </summary>
    public class InteractableObjectC : InteractableObject
    {
        [Header("Visual")]
        [SerializeField] private Color lockedColor = new(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color openColor = new(0.35f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color completedColor = new(0.25f, 0.45f, 0.9f, 1f);

        protected bool completed;
        private bool holdingMatchingA;

        public override InteractMode Mode => InteractMode.Trigger;
        public bool IsCompleted => completed;

        protected override void Awake()
        {
            base.Awake();
            ApplyVisual(false);
        }

        protected virtual void OnEnable()
        {
            EventManager.On<HeldItemInfo>(GameEvents.HeldItemChanged, OnHeldItemChanged);
            EventManager.On<string>(GameEvents.MechanismSatisfied, OnMechanismSatisfied);

            // 订阅前若已持有物品，补一次状态（事件不会重放）。
            PlayerInteractor player = Object.FindObjectOfType<PlayerInteractor>();
            holdingMatchingA = HasMatchingHeldItemA(player);
            RefreshVisual();
        }

        protected virtual void OnDisable()
        {
            EventManager.Off<HeldItemInfo>(GameEvents.HeldItemChanged, OnHeldItemChanged);
            EventManager.Off<string>(GameEvents.MechanismSatisfied, OnMechanismSatisfied);
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (!base.CanInteract(interactor))
                return false;

            if (completed)
                return false;

            return IsRequirementMet(interactor);
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            if (HasMatchingHeldItemA(interactor))
                interactor.ConsumeHeldItem();

            completed = true;
            ApplyVisual(false);
        }

        protected virtual void OnHeldItemChanged(HeldItemInfo info)
        {
            holdingMatchingA = info.HasItem
                && !string.IsNullOrEmpty(MechanismId)
                && string.Equals(info.MechanismId, MechanismId, System.StringComparison.Ordinal);

            RefreshVisual();
        }

        protected virtual void OnMechanismSatisfied(string mechanismId)
        {
            if (string.IsNullOrEmpty(MechanismId))
                return;

            if (!string.Equals(mechanismId, MechanismId, System.StringComparison.Ordinal))
                return;

            RefreshVisual();
        }

        protected virtual void RefreshVisual()
        {
            if (completed)
            {
                ApplyVisual(false);
                return;
            }

            bool open = holdingMatchingA || InteractableObjectB.IsMechanismSatisfied(MechanismId);
            ApplyVisual(open);
        }

        protected virtual bool IsRequirementMet(PlayerInteractor interactor)
        {
            if (HasMatchingHeldItemA(interactor))
                return true;

            return InteractableObjectB.IsMechanismSatisfied(MechanismId);
        }

        protected bool HasMatchingHeldItemA(PlayerInteractor interactor)
        {
            if (interactor == null || !interactor.HasHeldItem)
                return false;

            InteractableObject held = interactor.HeldItem;
            if (held == null || held.Mode != InteractMode.PickUp)
                return false;

            return MatchesMechanism(held);
        }

        protected void ApplyVisual(bool isOpen)
        {
            if (SpriteRenderer == null)
                return;

            if (completed)
                SpriteRenderer.color = completedColor;
            else if (isOpen)
                SpriteRenderer.color = openColor;
            else
                SpriteRenderer.color = lockedColor;
        }
    }
}

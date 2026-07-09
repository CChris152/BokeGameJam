using UnityEngine;

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

        private bool completed;
        private bool wasOpen;

        public override InteractMode Mode => InteractMode.Trigger;
        public bool IsCompleted => completed;

        protected override void Awake()
        {
            base.Awake();
            ApplyVisual(false);
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (completed)
                return false;

            bool open = IsRequirementMet(interactor);
            if (open != wasOpen)
            {
                wasOpen = open;
                ApplyVisual(open);
            }

            return open;
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            if (HasMatchingHeldItemA(interactor))
                interactor.ConsumeHeldItem();

            completed = true;
            wasOpen = true;
            ApplyVisual(true);
        }

        private bool IsRequirementMet(PlayerInteractor interactor)
        {
            if (HasMatchingHeldItemA(interactor))
                return true;

            return InteractableObjectB.IsMechanismSatisfied(MechanismId);
        }

        private bool HasMatchingHeldItemA(PlayerInteractor interactor)
        {
            if (interactor == null || !interactor.HasHeldItem)
                return false;

            InteractableObject held = interactor.HeldItem;
            if (held == null || held.Mode != InteractMode.PickUp)
                return false;

            return MatchesMechanism(held);
        }

        private void ApplyVisual(bool isOpen)
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

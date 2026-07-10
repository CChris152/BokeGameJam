using System;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Levels;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 花朵收集交付处（InteractableObjectC 变体）。
    /// 本体不显示多边形；玩家持花靠近时只显示 E 提示，按 E 交付。
    /// 持有红花 / 黄花时交付计数；持有非红非黄花时可互动但视为错误提交。
    /// </summary>
    public class InteractableObjectFlowerCollector : InteractableObjectC
    {
        [Header("Flower Collection")]
        [SerializeField] private int requiredRedFlowers = 1;
        [SerializeField] private int requiredYellowFlowers = 1;

        [Header("Level Transition")]
        [Tooltip("收集完成后是否自动进入下一关。第一关请关闭，由 Level1AnchorTriggers 统一判定通关。")]
        [SerializeField] private bool loadNextLevelOnComplete = false;

        private int collectedRed;
        private int collectedYellow;
        private bool levelTransitionStarted;

        /// <summary>持有非红非黄花并与交付点互动时触发（不消耗花朵）。</summary>
        public event Action WrongFlowerDeliveryAttempted;

        public int RequiredRedFlowers => Mathf.Max(0, requiredRedFlowers);
        public int RequiredYellowFlowers => Mathf.Max(0, requiredYellowFlowers);
        public int CollectedRed => collectedRed;
        public int CollectedYellow => collectedYellow;

        public override bool CanInteract(PlayerInteractor interactor)
        {
            // 不走 InteractableObjectC 的 IsRequirementMet，以便错误花也可互动。
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return false;

            if (completed)
                return false;

            return TryGetNeededHeldFlower(interactor, out _)
                || IsHoldingNonAcceptedFlower(interactor);
        }

        protected override bool ShouldShowInteractHint()
        {
            if (!base.ShouldShowInteractHint() || completed)
                return false;

            PlayerInteractor player = UnityEngine.Object.FindObjectOfType<PlayerInteractor>();
            return TryGetNeededHeldFlower(player, out _)
                || IsHoldingNonAcceptedFlower(player);
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (TryGetNeededHeldFlower(interactor, out FlowerColor color))
            {
                interactor.ConsumeHeldItem();

                if (color == FlowerColor.Red)
                    collectedRed++;
                else if (color == FlowerColor.Yellow)
                    collectedYellow++;
                else
                    return;

                if (IsCollectionComplete())
                {
                    completed = true;
                    RefreshInteractHint();
                    TryAdvanceToNextLevel();
                    return;
                }

                RefreshInteractHint();
                return;
            }

            if (!IsHoldingNonAcceptedFlower(interactor))
                return;

            WrongFlowerDeliveryAttempted?.Invoke();
        }

        protected override void OnHeldItemChanged(HeldItemInfo info)
        {
            RefreshInteractHint();
        }

        protected override void OnMechanismSatisfied(string mechanismId)
        {
            // Flower collector ignores A/B mechanism unlock.
        }

        protected override void RefreshVisual()
        {
            // 交付点本体不显示多边形，只靠 InteractHint + 碰撞触发。
            HideBodyVisual();
        }

        protected override void Awake()
        {
            base.Awake();
            HideBodyVisual();
        }

        private void HideBodyVisual()
        {
            if (SpriteRenderer != null)
                SpriteRenderer.enabled = false;
        }

        protected override bool IsRequirementMet(PlayerInteractor interactor)
        {
            return TryGetNeededHeldFlower(interactor, out _)
                || IsHoldingNonAcceptedFlower(interactor);
        }

        private void TryAdvanceToNextLevel()
        {
            if (!loadNextLevelOnComplete || levelTransitionStarted)
                return;

            levelTransitionStarted = true;

            LevelManager manager = LevelManager.EnsureExists();
            if (!manager.CompleteAndLoadNextLevel())
            {
                Debug.Log(
                    $"[InteractableObjectFlowerCollector] '{name}' 收集完成，但没有下一关可加载。",
                    this);
            }
        }

        private bool IsCollectionComplete()
        {
            return collectedRed >= RequiredRedFlowers
                && collectedYellow >= RequiredYellowFlowers;
        }

        private bool NeedsColor(FlowerColor color)
        {
            if (color == FlowerColor.Red)
                return collectedRed < RequiredRedFlowers;
            if (color == FlowerColor.Yellow)
                return collectedYellow < RequiredYellowFlowers;
            return false;
        }

        private static bool TryGetHeldFlower(PlayerInteractor interactor, out InteractableObjectFlower flower)
        {
            flower = null;
            if (interactor == null || !interactor.HasHeldItem)
                return false;

            flower = interactor.HeldItem as InteractableObjectFlower;
            return flower != null;
        }

        private bool TryGetNeededHeldFlower(PlayerInteractor interactor, out FlowerColor color)
        {
            color = default;
            if (!TryGetHeldFlower(interactor, out InteractableObjectFlower flower))
                return false;

            color = flower.ColorKind;
            return NeedsColor(color);
        }

        /// <summary>持有花，但颜色不是红/黄（紫、蓝等）。</summary>
        private static bool IsHoldingNonAcceptedFlower(PlayerInteractor interactor)
        {
            if (!TryGetHeldFlower(interactor, out InteractableObjectFlower flower))
                return false;

            FlowerColor color = flower.ColorKind;
            return color != FlowerColor.Red && color != FlowerColor.Yellow;
        }
    }
}

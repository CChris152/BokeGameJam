using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 花朵收集交付处（InteractableObjectC 变体）。
    /// 玩家持有红花 / 黄花时按 E 交付；数量达标后完成。
    /// </summary>
    public class InteractableObjectFlowerCollector : InteractableObjectC
    {
        [Header("Flower Collection")]
        [SerializeField] private int requiredRedFlowers = 1;
        [SerializeField] private int requiredYellowFlowers = 1;

        private int collectedRed;
        private int collectedYellow;

        public int RequiredRedFlowers => Mathf.Max(0, requiredRedFlowers);
        public int RequiredYellowFlowers => Mathf.Max(0, requiredYellowFlowers);
        public int CollectedRed => collectedRed;
        public int CollectedYellow => collectedYellow;

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (completed)
                return false;

            return TryGetNeededHeldFlower(interactor, out _);
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!TryGetNeededHeldFlower(interactor, out FlowerColor color))
                return;

            interactor.ConsumeHeldItem();

            if (color == FlowerColor.Red)
                collectedRed++;
            else
                collectedYellow++;

            if (IsCollectionComplete())
            {
                completed = true;
                ApplyVisual(false);
                return;
            }

            RefreshVisual();
        }

        protected override void OnHeldItemChanged(HeldItemInfo info)
        {
            RefreshVisual();
        }

        protected override void OnMechanismSatisfied(string mechanismId)
        {
            // Flower collector ignores A/B mechanism unlock.
        }

        protected override void RefreshVisual()
        {
            if (completed)
            {
                ApplyVisual(false);
                return;
            }

            PlayerInteractor player = Object.FindObjectOfType<PlayerInteractor>();
            bool open = TryGetNeededHeldFlower(player, out _);
            ApplyVisual(open);
        }

        protected override bool IsRequirementMet(PlayerInteractor interactor)
        {
            return TryGetNeededHeldFlower(interactor, out _);
        }

        private bool IsCollectionComplete()
        {
            return collectedRed >= RequiredRedFlowers
                && collectedYellow >= RequiredYellowFlowers;
        }

        private bool NeedsColor(FlowerColor color)
        {
            return color == FlowerColor.Red
                ? collectedRed < RequiredRedFlowers
                : collectedYellow < RequiredYellowFlowers;
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
    }
}

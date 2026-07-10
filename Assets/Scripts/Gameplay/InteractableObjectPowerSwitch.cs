using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 电闸：触发一次后广播 <see cref="GameEvents.PowerSwitchActivated"/>，之后不可再互动。
    /// </summary>
    public class InteractableObjectPowerSwitch : InteractableObject
    {
        [Header("Power Switch Visual")]
        [SerializeField] private Sprite activatedSprite;
        [SerializeField] private Color activatedColor = new(0.55f, 0.55f, 0.55f, 1f);

        private Sprite idleSprite;
        private Color idleColor = Color.white;
        private bool activated;

        public override InteractMode Mode => InteractMode.Trigger;
        public bool IsActivated => activated;

        protected override void Awake()
        {
            base.Awake();

            if (SpriteRenderer != null)
            {
                idleSprite = SpriteRenderer.sprite;
                idleColor = SpriteRenderer.color;
            }
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            return !activated;
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            activated = true;
            ApplyVisual();
            EventManager.Emit(GameEvents.PowerSwitchActivated);
        }

        private void ApplyVisual()
        {
            if (SpriteRenderer == null)
                return;

            if (activated)
            {
                if (activatedSprite != null)
                    SpriteRenderer.sprite = activatedSprite;
                SpriteRenderer.color = activatedColor;
            }
            else
            {
                SpriteRenderer.sprite = idleSprite;
                SpriteRenderer.color = idleColor;
            }
        }
    }
}

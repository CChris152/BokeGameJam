using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    public enum PowerGatedItemKind
    {
        Bear = 0,
        Camera = 1,
        Candy = 2,
        Flower = 3
    }

    /// <summary>
    /// 电闸锁定的可拾取物（熊 / 相机 / 糖果 / 花共用）。
    /// 开局不可互动；收到 <see cref="GameEvents.PowerSwitchActivated"/> 后才可捡起。
    /// 解锁且玩家进入交互范围时，在物体下方显示互动提示。
    /// </summary>
    public class InteractableObjectPowerGated : InteractableObject
    {
        private static bool powerSwitchActivated;

        [Header("Power Gate")]
        [SerializeField] private PowerGatedItemKind itemKind = PowerGatedItemKind.Bear;

        private bool unlocked;

        public PowerGatedItemKind ItemKind => itemKind;
        public bool IsUnlocked => unlocked;
        public static bool IsPowerSwitchActivated => powerSwitchActivated;

        private void OnEnable()
        {
            EventManager.On(GameEvents.PowerSwitchActivated, OnPowerSwitchActivated);

            // 事件不会重放：若电闸已启动，补一次解锁。
            SetUnlocked(powerSwitchActivated);
        }

        protected override void OnDisable()
        {
            EventManager.Off(GameEvents.PowerSwitchActivated, OnPowerSwitchActivated);
            base.OnDisable();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            return unlocked && base.CanInteract(interactor);
        }

        /// <summary>外部 / 调试：手动解锁（等同收到电闸启动）。</summary>
        public void UnlockFromPowerSwitch()
        {
            powerSwitchActivated = true;
            SetUnlocked(true);
        }

        private void OnPowerSwitchActivated()
        {
            powerSwitchActivated = true;
            SetUnlocked(true);
        }

        private void SetUnlocked(bool value)
        {
            unlocked = value;
            RefreshInteractHint();
        }

        protected override bool ShouldShowInteractHint()
        {
            return unlocked && base.ShouldShowInteractHint();
        }
    }
}

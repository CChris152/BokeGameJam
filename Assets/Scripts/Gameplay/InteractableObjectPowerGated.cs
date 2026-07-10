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
        private const string DefaultHintPrefabResourcePath = "Prefabs/Interactable/InteractHint";

        private static bool powerSwitchActivated;

        [Header("Power Gate")]
        [SerializeField] private PowerGatedItemKind itemKind = PowerGatedItemKind.Bear;

        [Header("Interact Hint")]
        [Tooltip("互动提示预制体；留空则尝试 Resources 路径。")]
        [SerializeField] private GameObject interactHintPrefab;
        [Tooltip("相对本物体的本地偏移（下方为正 Y 负值）。")]
        [SerializeField] private Vector2 hintLocalOffset = new(0f, -0.85f);
        [SerializeField] private string hintPrefabResourcePath = DefaultHintPrefabResourcePath;

        private bool unlocked;
        private bool isInRange;
        private GameObject hintInstance;

        public PowerGatedItemKind ItemKind => itemKind;
        public bool IsUnlocked => unlocked;
        public static bool IsPowerSwitchActivated => powerSwitchActivated;

        private void OnEnable()
        {
            EventManager.On(GameEvents.PowerSwitchActivated, OnPowerSwitchActivated);

            // 事件不会重放：若电闸已启动，补一次解锁。
            SetUnlocked(powerSwitchActivated);
            RefreshHint();
        }

        private void OnDisable()
        {
            EventManager.Off(GameEvents.PowerSwitchActivated, OnPowerSwitchActivated);
            isInRange = false;
            SetHintVisible(false);
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            return unlocked && base.CanInteract(interactor);
        }

        public override void SetInInteractRange(bool inRange)
        {
            isInRange = inRange;
            RefreshHint();
        }

        public override void PickUp(Transform holder)
        {
            base.PickUp(holder);
            isInRange = false;
            RefreshHint();
        }

        public override void Drop(Vector2 worldPosition)
        {
            base.Drop(worldPosition);
            RefreshHint();
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
            RefreshHint();
        }

        private void RefreshHint()
        {
            bool show = unlocked && isInRange && !IsHeld;
            SetHintVisible(show);
        }

        private void SetHintVisible(bool show)
        {
            if (!show)
            {
                if (hintInstance != null)
                    hintInstance.SetActive(false);
                return;
            }

            EnsureHintInstance();
            if (hintInstance == null)
                return;

            hintInstance.transform.localPosition = hintLocalOffset;
            hintInstance.SetActive(true);
        }

        private void EnsureHintInstance()
        {
            if (hintInstance != null)
                return;

            Transform existing = transform.Find("InteractHint");
            if (existing != null)
            {
                hintInstance = existing.gameObject;
                return;
            }

            GameObject prefab = interactHintPrefab;
            if (prefab == null && !string.IsNullOrWhiteSpace(hintPrefabResourcePath))
                prefab = Resources.Load<GameObject>(hintPrefabResourcePath.Trim());

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[InteractableObjectPowerGated] '{name}' 缺少互动提示预制体（interactHintPrefab / Resources '{hintPrefabResourcePath}'）。",
                    this);
                return;
            }

            hintInstance = Instantiate(prefab, transform);
            hintInstance.name = "InteractHint";
            hintInstance.transform.localPosition = hintLocalOffset;
            hintInstance.transform.localRotation = Quaternion.identity;
            hintInstance.transform.localScale = Vector3.one;
            hintInstance.SetActive(false);
        }
    }
}

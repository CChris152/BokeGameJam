using System.Collections.Generic;
using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 灯开关（InteractableObjectB 变体）：可反复拨动。
    /// 开灯时激活「开关开」，关灯时激活「开关关」；状态通过
    /// <see cref="GameEvents.LightsOffChanged"/> 广播，并与 <see cref="roomId"/> 绑定。
    /// 第一关灯光谜题完成后可由 <see cref="LockAllInteractions"/> 禁止再交互。
    /// </summary>
    public class InteractableObjectLightSwitch : InteractableObjectB
    {
        private static readonly List<InteractableObjectLightSwitch> allActive = new();
        private static bool interactionsLocked;
        private static bool levelStartedSubscribed;

        [Header("Light Switch")]
        [Tooltip("所属房间 id；须与该房间背景 BackgroundSpriteSwitcher.roomId 一致。")]
        [SerializeField] private string roomId = "room_1";
        [Tooltip("开局是否开灯。")]
        [SerializeField] private bool startLightsOn = true;

        [Header("Switch Visual")]
        [Tooltip("开灯时显示的物体（默认找子物体「开关开」）。")]
        [SerializeField] private GameObject switchOnObject;
        [Tooltip("关灯时显示的物体（默认找子物体「开关关」）。")]
        [SerializeField] private GameObject switchOffObject;

        private bool lightsOn;
        private bool initialized;

        public string RoomId => roomId != null ? roomId.Trim() : string.Empty;
        public bool LightsOn => lightsOn;
        public bool LightsOff => !lightsOn;
        public static bool InteractionsLocked => interactionsLocked;

        /// <summary>灯光谜题通关后调用：禁止所有灯开关交互并隐藏 E 提示。</summary>
        public static void LockAllInteractions()
        {
            if (interactionsLocked)
                return;

            interactionsLocked = true;
            RefreshAllHints();
        }

        /// <summary>新关卡开始时解除锁定。</summary>
        public static void ClearInteractionLock()
        {
            if (!interactionsLocked)
                return;

            interactionsLocked = false;
            RefreshAllHints();
        }

        protected override void Awake()
        {
            base.Awake();
            lightsOn = startLightsOn;
            ResolveVisualObjects();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!allActive.Contains(this))
                allActive.Add(this);

            EnsureLevelStartedSubscription();
        }

        protected override void OnDisable()
        {
            allActive.Remove(this);
            base.OnDisable();
        }

        private void Start()
        {
            initialized = true;
            ApplySwitchVisual();
            EmitLightsState();
        }

        private static void EnsureLevelStartedSubscription()
        {
            if (levelStartedSubscribed)
                return;

            levelStartedSubscribed = true;
            EventManager.On<string>(GameEvents.LevelStarted, OnLevelStarted);
        }

        private static void OnLevelStarted(string _)
        {
            ClearInteractionLock();
        }

        private static void RefreshAllHints()
        {
            for (int i = allActive.Count - 1; i >= 0; i--)
            {
                InteractableObjectLightSwitch sw = allActive[i];
                if (sw == null)
                {
                    allActive.RemoveAt(i);
                    continue;
                }

                sw.RefreshInteractHint();
            }
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (interactionsLocked)
                return false;

            return base.CanInteract(interactor);
        }

        protected override bool ShouldShowInteractHint()
        {
            if (interactionsLocked)
                return false;

            return base.ShouldShowInteractHint();
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            SetLightsOn(!lightsOn);

            if (GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.LightSwitch);
        }

        /// <summary>关卡编辑器 / 外部写入房间 id。</summary>
        public void ApplyRoomId(string newRoomId)
        {
            roomId = newRoomId != null ? newRoomId.Trim() : string.Empty;
            if (initialized)
                EmitLightsState();
        }

        public void SetLightsOn(bool on)
        {
            if (lightsOn == on && initialized)
            {
                ApplySwitchVisual();
                return;
            }

            lightsOn = on;
            ApplySwitchVisual();
            EmitLightsState();
        }

        private void EmitLightsState()
        {
            string id = RoomId;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning(
                    $"[InteractableObjectLightSwitch] '{name}' roomId 为空，关灯事件无法与房间绑定。",
                    this);
            }

            bool off = !lightsOn;
            RoomLightsState.Set(id, off);
            EventManager.Emit(GameEvents.LightsOffChanged, new RoomLightsInfo(id, off));
        }

        private void ResolveVisualObjects()
        {
            if (switchOnObject == null)
                switchOnObject = FindChildByName("开关开");
            if (switchOffObject == null)
                switchOffObject = FindChildByName("开关关");
        }

        private GameObject FindChildByName(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.gameObject : null;
        }

        private void ApplySwitchVisual()
        {
            if (switchOnObject != null)
                switchOnObject.SetActive(lightsOn);

            if (switchOffObject != null)
                switchOffObject.SetActive(!lightsOn);
        }
    }
}

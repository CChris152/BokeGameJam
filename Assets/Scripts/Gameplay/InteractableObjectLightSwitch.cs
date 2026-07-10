using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 灯开关（InteractableObjectB 变体）：可反复拨动。
    /// 开灯时激活「开关开」，关灯时激活「开关关」；状态通过
    /// <see cref="GameEvents.LightsOffChanged"/> 广播，并与 <see cref="roomId"/> 绑定。
    /// </summary>
    public class InteractableObjectLightSwitch : InteractableObjectB
    {
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

        protected override void Awake()
        {
            base.Awake();
            lightsOn = startLightsOn;
            ResolveVisualObjects();
        }

        private void Start()
        {
            initialized = true;
            ApplySwitchVisual();
            EmitLightsState();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            return true;
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            SetLightsOn(!lightsOn);
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

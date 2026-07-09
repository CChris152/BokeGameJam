using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 灯开关（InteractableObjectB 变体）：可反复拨动。
    /// 向下 = 开灯（on），向上 = 关灯（off）；状态通过
    /// <see cref="GameEvents.LightsOffChanged"/> 广播，并与 <see cref="roomId"/> 绑定。
    /// </summary>
    public class InteractableObjectLightSwitch : InteractableObjectB
    {
        [Header("Light Switch")]
        [Tooltip("所属房间 id；须与该房间背景 BackgroundSpriteSwitcher.roomId 一致。")]
        [SerializeField] private string roomId = "room_1";
        [Tooltip("开局是否开灯（向下）。")]
        [SerializeField] private bool startLightsOn = true;

        [Header("Lever Visual")]
        [Tooltip("关灯时（向上）的本地 Z 旋转。")]
        [SerializeField] private float offAngleZ = 0f;
        [Tooltip("开灯时（向下）的本地 Z 旋转。")]
        [SerializeField] private float onAngleZ = 180f;
        [SerializeField] private Sprite lightsOnSprite;
        [SerializeField] private Sprite lightsOffSprite;

        private bool lightsOn;
        private bool initialized;

        public string RoomId => roomId != null ? roomId.Trim() : string.Empty;
        public bool LightsOn => lightsOn;
        public bool LightsOff => !lightsOn;

        protected override void Awake()
        {
            base.Awake();
            lightsOn = startLightsOn;
        }

        private void Start()
        {
            initialized = true;
            ApplyLeverVisual();
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
                ApplyLeverVisual();
                return;
            }

            lightsOn = on;
            ApplyLeverVisual();
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

        private void ApplyLeverVisual()
        {
            Vector3 euler = transform.localEulerAngles;
            euler.z = lightsOn ? onAngleZ : offAngleZ;
            transform.localEulerAngles = euler;

            if (SpriteRenderer == null)
                return;

            Sprite sprite = lightsOn ? lightsOnSprite : lightsOffSprite;
            if (sprite != null)
                SpriteRenderer.sprite = sprite;
        }
    }
}

using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 挂在背景上：持有阳间 / 阴间 / 关灯三张 Sprite，
    /// 订阅 <see cref="GameEvents.ActiveWorldChanged"/> 与 <see cref="GameEvents.LightsOffChanged"/> 切换显示。
    /// 只响应同 <see cref="roomId"/> 的关灯事件；关灯优先于世界切换。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BackgroundSpriteSwitcher : MonoBehaviour
    {
        [Header("Room")]
        [Tooltip("所属房间 id；须与该房间灯开关 InteractableObjectLightSwitch.roomId 一致。")]
        [SerializeField] private string roomId = "room_1";

        [Header("Sprites")]
        [Tooltip("阳间（世界 A）")]
        [SerializeField] private Sprite livingWorldSprite;
        [Tooltip("阴间（世界 B）")]
        [SerializeField] private Sprite underworldSprite;
        [Tooltip("关灯")]
        [SerializeField] private Sprite lightsOffSprite;

        private SpriteRenderer spriteRenderer;
        private WorldId activeWorld = WorldId.A;
        private bool lightsOff;

        public string RoomId => roomId != null ? roomId.Trim() : string.Empty;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            EventManager.On<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);
            EventManager.On<RoomLightsInfo>(GameEvents.LightsOffChanged, OnLightsOffChanged);

            if (GameManager.Instance != null)
                activeWorld = GameManager.Instance.ActiveWorld;

            if (RoomLightsState.TryGet(RoomId, out bool off))
                lightsOff = off;

            ApplySprite();
        }

        private void OnDisable()
        {
            EventManager.Off<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);
            EventManager.Off<RoomLightsInfo>(GameEvents.LightsOffChanged, OnLightsOffChanged);
        }

        private void OnActiveWorldChanged(WorldId world)
        {
            activeWorld = world;
            ApplySprite();
        }

        private void OnLightsOffChanged(RoomLightsInfo info)
        {
            string selfId = RoomId;
            if (string.IsNullOrEmpty(selfId))
                return;

            if (!string.Equals(info.RoomId, selfId, System.StringComparison.Ordinal))
                return;

            lightsOff = info.LightsOff;
            ApplySprite();
        }

        private void ApplySprite()
        {
            if (spriteRenderer == null)
                return;

            Sprite next;
            if (lightsOff)
                next = lightsOffSprite;
            else if (activeWorld == WorldId.B)
                next = underworldSprite;
            else
                next = livingWorldSprite;

            if (next != null)
                spriteRenderer.sprite = next;
            else
                Debug.LogWarning(
                    $"[BackgroundSpriteSwitcher] Missing sprite for room='{RoomId}', lightsOff={lightsOff}, world={activeWorld}.",
                    this);
        }
    }
}

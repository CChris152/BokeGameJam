using System.Collections.Generic;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Levels;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 路灯（InteractableObjectB 变体）。
    /// 表世界开局熄灭，按 E 切换本地小光源与亮/关灯贴图。
    /// 里世界仅作为位置标记，默认不可交互。
    /// 监听 <see cref="GameEvents.WallLampSequenceCompleted"/>：按 X 从左到右编号 1..N，
    /// 玩家须按壁灯闪烁顺序开灯；全对则通关 Level2，错则全部灭灯。
    /// </summary>
    public class InteractableObjectStreetLamp : InteractableObjectB
    {
        private const string OffSpriteResourcePath = "Art/Pictures/关灯";
        private const string OnSpriteResourcePath = "Art/Pictures/亮灯";
        private const string Level2Id = "level_2";

        [Header("Street Lamp")]
        [Tooltip("亮灯时显示的小光源物体（默认找子物体 LightGlow）。")]
        [SerializeField] private GameObject lightGlowObject;
        [Tooltip("仅在表世界（World A）可交互；里世界只作标记。")]
        [SerializeField] private bool interactOnlyInOuterWorld = true;

        [Header("Lamp Sprites")]
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Sprite onSprite;

        [Header("Sequence Success Audio")]
        [SerializeField] private AudioClip sequenceSuccessClip;
        [Tooltip("可选：走 GameAudioManager 的 SFX id；优先使用上面的 AudioClip。")]
        [SerializeField] private string sequenceSuccessSfxId;

        private bool wasActivated;
        private AudioSource localAudioSource;
        private int lampNumber;

        private static readonly List<InteractableObjectStreetLamp> registered = new();
        private static int[] expectedOrder = System.Array.Empty<int>();
        private static int progress;
        private static bool puzzleArmed;
        private static bool puzzleCompleted;
        private static int lastRegisterFrame = -1;

        private static AudioClip fallbackBeepClip;
        private static Sprite cachedOffSprite;
        private static Sprite cachedOnSprite;

        /// <summary>按 X 从左到右的 1-based 编号；未注册时为 0。</summary>
        public int LampNumber => lampNumber;

        protected override void Awake()
        {
            base.Awake();
            ResolveLightGlow();
            EnsureLampSprites();
            wasActivated = IsActivated;
            ApplyLampState();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventManager.On<int[]>(GameEvents.WallLampSequenceCompleted, OnWallLampSequenceCompleted);
        }

        protected override void OnDisable()
        {
            EventManager.Off<int[]>(GameEvents.WallLampSequenceCompleted, OnWallLampSequenceCompleted);
            registered.Remove(this);
            base.OnDisable();
        }

        private void LateUpdate()
        {
            if (wasActivated == IsActivated)
                return;

            wasActivated = IsActivated;
            ApplyLampState();
        }

        private void OnWallLampSequenceCompleted(int[] flashOrder)
        {
            RegisterPuzzleFromFlashOrder(flashOrder);
        }

        private static void RegisterPuzzleFromFlashOrder(int[] flashOrder)
        {
            // 每个路灯都会收到同一事件；同帧只注册一次。
            if (Time.frameCount == lastRegisterFrame)
                return;
            lastRegisterFrame = Time.frameCount;

            InteractableObjectStreetLamp[] lamps = Object.FindObjectsOfType<InteractableObjectStreetLamp>();
            System.Array.Sort(lamps, CompareByXThenId);

            registered.Clear();
            for (int i = 0; i < lamps.Length; i++)
            {
                InteractableObjectStreetLamp lamp = lamps[i];
                if (lamp == null)
                    continue;

                lamp.lampNumber = i + 1;
                lamp.SetActivated(false);
                lamp.wasActivated = false;
                registered.Add(lamp);
            }

            expectedOrder = flashOrder != null && flashOrder.Length > 0
                ? (int[])flashOrder.Clone()
                : System.Array.Empty<int>();
            progress = 0;
            puzzleCompleted = false;
            puzzleArmed = expectedOrder.Length > 0 && registered.Count > 0;

            if (!puzzleArmed)
            {
                Debug.LogWarning("[InteractableObjectStreetLamp] Wall lamp sequence completed but puzzle could not be armed.");
                return;
            }

            Debug.Log(
                $"[InteractableObjectStreetLamp] Registered {registered.Count} lamps by X; expected order=[{string.Join(",", expectedOrder)}]");
        }

        private static int CompareByXThenId(InteractableObjectStreetLamp a, InteractableObjectStreetLamp b)
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            int cmp = a.transform.position.x.CompareTo(b.transform.position.x);
            if (cmp != 0)
                return cmp;

            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return false;

            if (interactOnlyInOuterWorld && IsInUnderworld())
                return false;

            if (puzzleCompleted)
                return false;

            if (puzzleArmed)
                return !IsActivated;

            // 表演前：可自由开关。
            if (IsActivated)
                return true;

            return base.CanInteract(interactor);
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            if (!puzzleArmed)
            {
                if (IsActivated)
                {
                    SetActivated(false);
                    wasActivated = false;
                    return;
                }

                base.OnInteract(interactor);
                wasActivated = IsActivated;
                return;
            }

            TryActivateInPuzzleOrder();
        }

        private void TryActivateInPuzzleOrder()
        {
            if (puzzleCompleted || expectedOrder == null || progress >= expectedOrder.Length)
                return;

            int expectedLamp = expectedOrder[progress];
            if (lampNumber != expectedLamp)
            {
                TurnOffAllRegistered();
                progress = 0;
                return;
            }

            SetActivated(true);
            wasActivated = true;
            progress++;

            if (progress < expectedOrder.Length)
                return;

            CompleteLevel2Puzzle();
        }

        private static void TurnOffAllRegistered()
        {
            for (int i = 0; i < registered.Count; i++)
            {
                InteractableObjectStreetLamp lamp = registered[i];
                if (lamp == null)
                    continue;

                lamp.SetActivated(false);
                lamp.wasActivated = false;
            }
        }

        private void CompleteLevel2Puzzle()
        {
            if (puzzleCompleted)
                return;

            puzzleCompleted = true;
            PlaySequenceSuccessAudio();

            LevelManager levelManager = LevelManager.Instance;
            if (levelManager != null && levelManager.HasCurrentLevel)
                levelManager.CompleteCurrentLevel();
            else
                EventManager.Emit(GameEvents.LevelCompleted, Level2Id);

            Debug.Log("[InteractableObjectStreetLamp] Street lamp sequence correct — Level2 completed.", this);
        }

        protected override void ApplyVisual()
        {
            ApplyLampState();
        }

        private void ResolveLightGlow()
        {
            if (lightGlowObject != null)
                return;

            Transform child = transform.Find("LightGlow");
            if (child != null)
                lightGlowObject = child.gameObject;
        }

        private void EnsureLampSprites()
        {
            if (offSprite == null)
            {
                if (cachedOffSprite == null)
                    cachedOffSprite = Resources.Load<Sprite>(OffSpriteResourcePath);
                offSprite = cachedOffSprite;
            }

            if (onSprite == null)
            {
                if (cachedOnSprite == null)
                    cachedOnSprite = Resources.Load<Sprite>(OnSpriteResourcePath);
                onSprite = cachedOnSprite;
            }

            if (offSprite == null)
                Debug.LogError($"[InteractableObjectStreetLamp] Missing sprite at Resources/{OffSpriteResourcePath}", this);
            if (onSprite == null)
                Debug.LogError($"[InteractableObjectStreetLamp] Missing sprite at Resources/{OnSpriteResourcePath}", this);
        }

        private void ApplyLampState()
        {
            EnsureLampSprites();

            if (SpriteRenderer != null)
            {
                Sprite target = IsActivated ? onSprite : offSprite;
                if (target != null)
                    SpriteRenderer.sprite = target;
            }

            if (lightGlowObject != null)
                lightGlowObject.SetActive(IsActivated);
        }

        private void PlaySequenceSuccessAudio()
        {
            EnsureLocalAudioSource();

            if (sequenceSuccessClip != null)
            {
                localAudioSource.PlayOneShot(sequenceSuccessClip);
                return;
            }

            if (!string.IsNullOrWhiteSpace(sequenceSuccessSfxId) && GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlaySFXById(sequenceSuccessSfxId.Trim());
                return;
            }

            localAudioSource.PlayOneShot(GetFallbackBeepClip());
        }

        private void EnsureLocalAudioSource()
        {
            if (localAudioSource != null)
                return;

            localAudioSource = GetComponent<AudioSource>();
            if (localAudioSource == null)
            {
                localAudioSource = gameObject.AddComponent<AudioSource>();
                localAudioSource.playOnAwake = false;
            }
        }

        private static AudioClip GetFallbackBeepClip()
        {
            if (fallbackBeepClip != null)
                return fallbackBeepClip;

            const int sampleRate = 44100;
            const float duration = 0.18f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - (t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * envelope * 0.35f;
            }

            fallbackBeepClip = AudioClip.Create("StreetLampSequenceBeep", sampleCount, 1, sampleRate, false);
            fallbackBeepClip.SetData(samples, 0);
            return fallbackBeepClip;
        }

        private static bool IsInUnderworld()
        {
            return GameManager.Instance != null && GameManager.Instance.IsInUnderworld;
        }
    }
}

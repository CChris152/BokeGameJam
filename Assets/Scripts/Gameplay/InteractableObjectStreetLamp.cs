using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using BokeGameJam.Core;
using BokeGameJam.Data;
using BokeGameJam.Levels;
using BokeGameJam.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 路灯（InteractableObjectB 变体）。
    /// 表世界开局熄灭，按 E 切换本地小光源与亮/关灯贴图。
    /// 里世界仅作为位置标记，默认不可交互。
    /// 监听 <see cref="GameEvents.WallLampSequenceCompleted"/>：按 X 从左到右编号 1..N，
    /// 灯头上方以罗马数字显示编号；玩家须按壁灯闪烁顺序开灯；全对则通关 Level2，错则全部灭灯。
    /// </summary>
    public class InteractableObjectStreetLamp : InteractableObjectB
    {
        private const string OffSpriteResourcePath = "Art/Pictures/关灯";
        private const string OnSpriteResourcePath = "Art/Pictures/亮灯";
        private const string Level2Id = "level_2";
        private const string StartSceneId = "StartScene";
        private const string DefaultWrongOrderStoryPath = "ScriptableObjects/Stories/Story16";
        private const string DefaultAllLitStoryPath = "ScriptableObjects/Stories/Story17";
        private const string DefaultNumberFontResourcePath = "Art/Fonts/FZFENGRSTJW-EB SDF";

        private static readonly string[] RomanNumerals =
        {
            "I", "II", "III", "IV", "V", "VI",
            "VII", "VIII", "IX", "X", "XI", "XII"
        };

        [Header("Street Lamp")]
        [Tooltip("亮灯时显示的小光源物体（默认找子物体 LightGlow）。")]
        [SerializeField] private GameObject lightGlowObject;
        [Tooltip("仅在表世界（World A）可交互；里世界只作标记。")]
        [SerializeField] private bool interactOnlyInOuterWorld = true;
        [Tooltip("灯头上方的编号（TextMeshPro）；注册时写入罗马数字。")]
        [SerializeField] private TMP_Text numberLabel;

        [Header("Lamp Sprites")]
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Sprite onSprite;

        [Header("Sequence Success Audio")]
        [SerializeField] private AudioClip sequenceSuccessClip;
        [Tooltip("可选：走 GameAudioManager 的 SFX id；优先使用上面的 AudioClip。")]
        [SerializeField] private string sequenceSuccessSfxId;

        [Header("Puzzle Stories")]
        [Tooltip("错序灭灯时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence wrongOrderStory;
        [SerializeField] private string wrongOrderStoryResourcePath = DefaultWrongOrderStoryPath;
        [Tooltip("全亮通关时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence allLitStory;
        [SerializeField] private string allLitStoryResourcePath = DefaultAllLitStoryPath;

        private bool wasActivated;
        private AudioSource localAudioSource;
        private int lampNumber;

        private static readonly List<InteractableObjectStreetLamp> registered = new();
        private static int[] expectedOrder = System.Array.Empty<int>();
        private static int progress;
        private static bool puzzleArmed;
        private static bool puzzleCompleted;
        private static int lastRegisterFrame = -1;
        private static bool hasPlayedAllLitStory;
        private static bool hasStartedEndingMedia;

        private static Sprite cachedOffSprite;
        private static Sprite cachedOnSprite;

        /// <summary>按 X 从左到右的 1-based 编号；未注册时为 0。</summary>
        public int LampNumber => lampNumber;

        protected override void Awake()
        {
            base.Awake();
            ResolveLightGlow();
            ResolveNumberLabel();
            EnsureLampSprites();
            wasActivated = IsActivated;
            ApplyLampState();
            RefreshNumberLabel();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventManager.On<int[]>(GameEvents.WallLampSequenceCompleted, OnWallLampSequenceCompleted);
            EventManager.On<string>(GameEvents.LevelCompleted, OnLevelCompleted);
        }

        protected override void OnDisable()
        {
            EventManager.Off<int[]>(GameEvents.WallLampSequenceCompleted, OnWallLampSequenceCompleted);
            EventManager.Off<string>(GameEvents.LevelCompleted, OnLevelCompleted);
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

        /// <summary>Level2 通关事件触发 Story17（路灯全亮剧情）。</summary>
        private void OnLevelCompleted(string levelId)
        {
            if (!string.Equals(levelId, Level2Id, System.StringComparison.Ordinal))
                return;

            // 同帧内 GameManager 可能已 Destroy 旧 Banner；延后一帧再播，确保 UI 可用。
            StartCoroutine(PlayAllLitStoryNextFrame());
        }

        private IEnumerator PlayAllLitStoryNextFrame()
        {
            yield return null;
            TryPlayAllLitStory();
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
                lamp.RefreshNumberLabel();
                registered.Add(lamp);
            }

            expectedOrder = flashOrder != null && flashOrder.Length > 0
                ? (int[])flashOrder.Clone()
                : System.Array.Empty<int>();
            progress = 0;
            puzzleCompleted = false;
            hasPlayedAllLitStory = false;
            hasStartedEndingMedia = false;
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

            if (GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.LightSwitch);

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
                PlayBannerStory(ResolveWrongOrderStory(), "路灯错序剧情");
                PlaySequenceFailureAudio();
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

        private void TryPlayAllLitStory()
        {
            if (hasPlayedAllLitStory)
                return;

            hasPlayedAllLitStory = true;
            PlayBannerStory(ResolveAllLitStory(), "路灯全亮剧情", StartLevel2EndingMedia);
        }

        private StorySequence ResolveWrongOrderStory()
        {
            return ResolveStory(wrongOrderStory, wrongOrderStoryResourcePath);
        }

        private StorySequence ResolveAllLitStory()
        {
            return ResolveStory(allLitStory, allLitStoryResourcePath);
        }

        private static StorySequence ResolveStory(StorySequence assigned, string resourcePath)
        {
            if (assigned != null)
                return assigned;

            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            return Resources.Load<StorySequence>(resourcePath.Trim());
        }

        private void PlayBannerStory(StorySequence story, string storyLabel, System.Action onComplete = null)
        {
            if (story == null || !story.HasLines)
            {
                Debug.LogWarning($"[InteractableObjectStreetLamp] {storyLabel}配置缺失或为空。", this);
                onComplete?.Invoke();
                return;
            }

            CameraTopBannerUI banner = EnsureTopBanner();
            if (banner == null)
            {
                Debug.LogWarning(
                    $"[InteractableObjectStreetLamp] CameraTopBannerUI 未找到，无法播放{storyLabel}。",
                    this);
                onComplete?.Invoke();
                return;
            }

            banner.PlayStory(story.CreateLineList(), onComplete);
        }

        /// <summary>Story17 播完后：黑屏媒体序列，结束后回主菜单。</summary>
        private void StartLevel2EndingMedia()
        {
            if (hasStartedEndingMedia)
                return;

            hasStartedEndingMedia = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ClosePauseMenuIfOpen();
                GameManager.Instance.SetPauseMenuEscEnabled(false);
            }

            BlackScreenMediaPlayer mediaPlayer = BlackScreenMediaPlayer.Instance
                ?? BlackScreenMediaPlayer.EnsureExists();

            if (mediaPlayer == null)
            {
                Debug.LogWarning(
                    "[InteractableObjectStreetLamp] BlackScreenMediaPlayer 缺失，直接回主菜单。",
                    this);
                ReturnToMainMenu();
                return;
            }

            mediaPlayer.Play(BlackScreenMediaPlayer.PresetLevel2ToMainMenu, ReturnToMainMenu);
        }

        private static void ReturnToMainMenu()
        {
            // 黑屏回调中先还原表世界（与暂停回主菜单 / MainMenu 状态一致），再切场景。
            if (GameManager.Instance != null)
                GameManager.Instance.ResetToLivingWorld();

            if (GameSceneManager.Instance != null)
            {
                GameSceneManager.Instance.LoadSceneById(StartSceneId);
                return;
            }

            Debug.LogError("[InteractableObjectStreetLamp] GameSceneManager missing, cannot return to main menu.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(StartSceneId);
        }

        private static CameraTopBannerUI EnsureTopBanner()
        {
            CameraTopBannerUI banner = CameraTopBannerUI.Instance;
            if (banner != null)
                return banner;

            if (UIManager.Instance == null)
                return null;

            GameObject go = UIManager.Instance.Load(CameraTopBannerUI.ResourceId);
            return go != null ? go.GetComponent<CameraTopBannerUI>() : null;
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

        private void ResolveNumberLabel()
        {
            if (numberLabel != null)
                return;

            Transform child = transform.Find("NumberLabel");
            if (child != null)
                numberLabel = child.GetComponent<TMP_Text>();
        }

        private void RefreshNumberLabel()
        {
            ResolveNumberLabel();
            if (numberLabel == null)
                return;

            if (numberLabel.font == null)
            {
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>(DefaultNumberFontResourcePath);
                if (font != null)
                    numberLabel.font = font;
            }

            if (lampNumber <= 0)
            {
                numberLabel.text = string.Empty;
                numberLabel.gameObject.SetActive(false);
                return;
            }

            numberLabel.text = ToRomanNumeral(lampNumber);
            numberLabel.gameObject.SetActive(true);
        }

        private static string ToRomanNumeral(int value)
        {
            if (value <= 0)
                return string.Empty;

            if (value <= RomanNumerals.Length)
                return RomanNumerals[value - 1];

            return value.ToString();
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

            // LevelCompleted event supplies the approved completion cue.
        }

        private static void PlaySequenceFailureAudio()
        {
            if (GameAudioManager.Instance == null)
                return;

            GameAudioManager.Instance.PlayRandomSFXByResourcePaths(
                1f,
                GameSfxPaths.PuzzleFailure1,
                GameSfxPaths.PuzzleFailure3);
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

        private static bool IsInUnderworld()
        {
            return GameManager.Instance != null && GameManager.Instance.IsInUnderworld;
        }
    }
}

using System.Collections;
using BokeGameJam.CameraSystem;
using BokeGameJam.Core;
using BokeGameJam.Data;
using BokeGameJam.Input;
using BokeGameJam.Levels;
using BokeGameJam.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// Level1 分段相机：Start 固定到 Place1；
    /// 玩家 X 从左到右越过 Anchor1 → 平滑移到 Place2；
    /// 越过 Anchor2 → Place3；反向越过则反向切换。
    /// 子物体命名：Place1 / Place2 / Place3 / Anchor1 / Anchor2（也可在 Inspector 拖引用）。
    /// 开场 / 首次摘花 / 首次 Shift / 里世界首次到达 Place2 / 首次交付红黄花 / 错误交花 /
    /// 首次与鬼魂互动 / 再次与鬼魂互动 / 灯光正确(Story9) / 通关(Story10) 剧情；
    /// 通关需同时满足：红黄花交付完成 + 表世界灯光（卧室亮、客厅暗、厨房亮）。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class Level1AnchorTriggers : MonoBehaviour
    {
        private const string TargetSceneName = "Level1";
        private const string DefaultIntroStoryResourcePath = "ScriptableObjects/Stories/Story1";
        private const string DefaultFirstFlowerStoryResourcePath = "ScriptableObjects/Stories/Story2";
        private const string DefaultFirstShiftStoryResourcePath = "ScriptableObjects/Stories/Story3";
        private const string DefaultPlace2StoryResourcePath = "ScriptableObjects/Stories/Story4";
        private const string DefaultFirstFlowerDeliveryStoryResourcePath = "ScriptableObjects/Stories/Story5";
        private const string DefaultWrongFlowerDeliveryStoryResourcePath = "ScriptableObjects/Stories/Story6";
        private const string DefaultFirstGhostInteractStoryResourcePath = "ScriptableObjects/Stories/Story7";
        private const string DefaultRepeatGhostInteractStoryResourcePath = "ScriptableObjects/Stories/Story8";
        private const string DefaultLightsCorrectStoryResourcePath = "ScriptableObjects/Stories/Story9";
        private const string DefaultLevelClearStoryResourcePath = "ScriptableObjects/Stories/Story10";

        [Header("Camera Places (可拖拽，留空则按子物体名查找)")]
        [SerializeField] private Transform place1;
        [SerializeField] private Transform place2;
        [SerializeField] private Transform place3;

        [Header("X Cross Anchors")]
        [SerializeField] private Transform anchor1;
        [SerializeField] private Transform anchor2;

        [Header("Move")]
        [SerializeField] private float cameraMoveDuration = 0.6f;
        [Tooltip("相机目标 Y 额外偏移（与 CameraManager followOffset.y 类似时可填 1.5）")]
        [SerializeField] private float cameraYOffset;

        [Header("Intro Story")]
        [Tooltip("开场剧情配置；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence introStory;
        [SerializeField] private string introStoryResourcePath = DefaultIntroStoryResourcePath;
        [SerializeField] private bool playIntroStoryOnStart = true;

        [Header("First Flower Story")]
        [Tooltip("本关第一次捡到任意颜色花时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence firstFlowerStory;
        [SerializeField] private string firstFlowerStoryResourcePath = DefaultFirstFlowerStoryResourcePath;
        [SerializeField] private bool playStoryOnFirstFlowerPickup = true;

        [Header("First Shift Story")]
        [Tooltip("本关第一次按 Shift（切换世界）时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence firstShiftStory;
        [SerializeField] private string firstShiftStoryResourcePath = DefaultFirstShiftStoryResourcePath;
        [SerializeField] private bool playStoryOnFirstShift = true;

        [Header("Place2 Story")]
        [Tooltip("本关在里世界第一次到达 Place2 的 X 时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence place2Story;
        [SerializeField] private string place2StoryResourcePath = DefaultPlace2StoryResourcePath;
        [SerializeField] private bool playStoryOnFirstReachPlace2 = true;

        [Header("First Flower Delivery Story")]
        [Tooltip("本关第一次成功交付红花或黄花时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence firstFlowerDeliveryStory;
        [SerializeField] private string firstFlowerDeliveryStoryResourcePath = DefaultFirstFlowerDeliveryStoryResourcePath;
        [SerializeField] private bool playStoryOnFirstFlowerDelivery = true;

        [Header("Wrong Flower Delivery Story")]
        [Tooltip("持有非红非黄花与交付点互动时播放（每次都触发）；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence wrongFlowerDeliveryStory;
        [SerializeField] private string wrongFlowerDeliveryStoryResourcePath = DefaultWrongFlowerDeliveryStoryResourcePath;
        [SerializeField] private bool playStoryOnWrongFlowerDelivery = true;

        [Header("First Ghost Interact Story")]
        [Tooltip("本关第一次在里世界与鬼魂互动时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence firstGhostInteractStory;
        [SerializeField] private string firstGhostInteractStoryResourcePath = DefaultFirstGhostInteractStoryResourcePath;
        [SerializeField] private bool playStoryOnFirstGhostInteract = true;

        [Header("Repeat Ghost Interact Story")]
        [Tooltip("本关在里世界第二次及以后与鬼魂互动时播放（每次都触发）；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence repeatGhostInteractStory;
        [SerializeField] private string repeatGhostInteractStoryResourcePath = DefaultRepeatGhostInteractStoryResourcePath;
        [SerializeField] private bool playStoryOnRepeatGhostInteract = true;

        [Header("Lights Correct Story")]
        [Tooltip("三房间灯光首次达到正确状态时播放 Story9；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence lightsCorrectStory;
        [SerializeField] private string lightsCorrectStoryResourcePath = DefaultLightsCorrectStoryResourcePath;
        [SerializeField] private bool playStoryOnLightsCorrect = true;

        [Header("Level Clear Story")]
        [Tooltip("正式通关前播放 Story10，播完后进入下一关；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence levelClearStory;
        [SerializeField] private string levelClearStoryResourcePath = DefaultLevelClearStoryResourcePath;
        [SerializeField] private bool playStoryOnLevelClear = true;

        [Header("Level Clear Conditions")]
        [Tooltip("红黄花交付完成后为 true；由本脚本轮询 FlowerCollector。")]
        [SerializeField] private bool flowersDelivered;
        [SerializeField] private string bedroomRoomId = "room_1";
        [SerializeField] private string livingRoomId = "room_2";
        [SerializeField] private string kitchenRoomId = "room_3";
        [Tooltip("目标：卧室亮、客厅暗、厨房亮。")]
        [SerializeField] private bool requireBedroomLightsOn = true;
        [SerializeField] private bool requireLivingRoomLightsOn = false;
        [SerializeField] private bool requireKitchenLightsOn = true;

        private PlayerController player;
        private PlayerInteractor playerInteractor;
        private InteractableObjectFlowerCollector flowerCollector;
        private float previousPlayerX;
        private bool hasPreviousPlayerX;
        private Coroutine moveRoutine;
        private Coroutine introStoryRoutine;
        private Coroutine clearSequenceRoutine;
        private bool sceneReady;
        private Transform currentPlace;
        private bool hasPlayedFirstFlowerStory;
        private bool hasPlayedFirstShiftStory;
        private bool hasPlayedPlace2Story;
        private bool hasPlayedFirstFlowerDeliveryStory;
        private bool hasPlayedLightsCorrectStory;
        private int underworldGhostInteractCount;
        private bool flowerCollectorBound;
        private bool clearSequenceRunning;
        private bool levelAdvanceStarted;

        private void Awake()
        {
            sceneReady = SceneManager.GetActiveScene().name == TargetSceneName;
            if (!sceneReady)
            {
                Debug.LogWarning(
                    $"[Level1AnchorTriggers] 当前场景不是 {TargetSceneName}，组件已禁用。",
                    this);
                enabled = false;
                return;
            }

            ResolveReferences();
        }

        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name != TargetSceneName)
                return;

            EventManager.On(InputEvents.WorldToggle, OnWorldTogglePressed);
            EventManager.On<RoomLightsInfo>(GameEvents.LightsOffChanged, OnLightsOffChanged);
            EventManager.On<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);
            InteractableObjectD.Interacted += OnGhostInteracted;
            TryBindFlowerCollector();
        }

        private void Start()
        {
            if (!sceneReady)
                return;

            // 停掉跟随，避免与分段固定相机抢控制权。
            if (CameraManager.Instance != null)
                CameraManager.Instance.SetFollowTarget(null);

            SnapCameraTo(place1);
            currentPlace = place1;

            if (playIntroStoryOnStart)
                introStoryRoutine = StartCoroutine(PlayStoryNextFrame(ResolveIntroStory(), "开场剧情"));
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.WorldToggle, OnWorldTogglePressed);
            EventManager.Off<RoomLightsInfo>(GameEvents.LightsOffChanged, OnLightsOffChanged);
            EventManager.Off<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);
            InteractableObjectD.Interacted -= OnGhostInteracted;
            UnbindFlowerCollector();

            if (introStoryRoutine != null)
            {
                StopCoroutine(introStoryRoutine);
                introStoryRoutine = null;
            }

            if (clearSequenceRoutine != null)
            {
                StopCoroutine(clearSequenceRoutine);
                clearSequenceRoutine = null;
            }

            clearSequenceRunning = false;
        }

        /// <summary>等一帧，确保 GameManager 已加载 CameraTopBanner 后再播剧情。</summary>
        private IEnumerator PlayStoryNextFrame(StorySequence story, string storyLabel)
        {
            yield return null;
            PlayStoryNow(story, storyLabel);
            introStoryRoutine = null;
        }

        private void PlayStoryNow(StorySequence story, string storyLabel)
        {
            if (story == null || !story.HasLines)
            {
                Debug.LogWarning($"[Level1AnchorTriggers] {storyLabel}配置缺失或为空。", this);
                return;
            }

            CameraTopBannerUI banner = CameraTopBannerUI.Instance;
            if (banner == null && UIManager.Instance != null)
                banner = UIManager.Instance.ShowTopBanner();

            if (banner == null)
            {
                Debug.LogWarning(
                    $"[Level1AnchorTriggers] CameraTopBannerUI 未找到，无法播放{storyLabel}。",
                    this);
                return;
            }

            banner.PlayStory(story.CreateLineList());
        }

        private static StorySequence ResolveStory(StorySequence assigned, string resourcePath)
        {
            if (assigned != null)
                return assigned;

            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            return Resources.Load<StorySequence>(resourcePath.Trim());
        }

        private StorySequence ResolveIntroStory()
        {
            return ResolveStory(introStory, introStoryResourcePath);
        }

        private StorySequence ResolveFirstFlowerStory()
        {
            return ResolveStory(firstFlowerStory, firstFlowerStoryResourcePath);
        }

        private StorySequence ResolveFirstShiftStory()
        {
            return ResolveStory(firstShiftStory, firstShiftStoryResourcePath);
        }

        private StorySequence ResolvePlace2Story()
        {
            return ResolveStory(place2Story, place2StoryResourcePath);
        }

        private StorySequence ResolveFirstFlowerDeliveryStory()
        {
            return ResolveStory(firstFlowerDeliveryStory, firstFlowerDeliveryStoryResourcePath);
        }

        private StorySequence ResolveWrongFlowerDeliveryStory()
        {
            return ResolveStory(wrongFlowerDeliveryStory, wrongFlowerDeliveryStoryResourcePath);
        }

        private StorySequence ResolveFirstGhostInteractStory()
        {
            return ResolveStory(firstGhostInteractStory, firstGhostInteractStoryResourcePath);
        }

        private StorySequence ResolveRepeatGhostInteractStory()
        {
            return ResolveStory(repeatGhostInteractStory, repeatGhostInteractStoryResourcePath);
        }

        private StorySequence ResolveLightsCorrectStory()
        {
            return ResolveStory(lightsCorrectStory, lightsCorrectStoryResourcePath);
        }

        private StorySequence ResolveLevelClearStory()
        {
            return ResolveStory(levelClearStory, levelClearStoryResourcePath);
        }

        /// <summary>本关第一次按 Shift（世界切换）时播放 Story3。</summary>
        private void OnWorldTogglePressed()
        {
            if (!sceneReady || !playStoryOnFirstShift || hasPlayedFirstShiftStory)
                return;

            hasPlayedFirstShiftStory = true;
            PlayStoryNow(ResolveFirstShiftStory(), "首次Shift剧情");
        }

        private void Update()
        {
            if (!sceneReady)
                return;

            if (!TryGetPlayer(out Transform playerTransform))
                return;

            TryTriggerFirstFlowerStory();
            TryTriggerFirstFlowerDeliveryStory();
            TrySyncFlowerDeliveryCondition();
            EvaluateLightsAndClearStories();

            float x = playerTransform.position.x;
            if (!hasPreviousPlayerX)
            {
                previousPlayerX = x;
                hasPreviousPlayerX = true;
                return;
            }

            float prevX = previousPlayerX;
            previousPlayerX = x;

            TryTriggerPlace2Story(prevX, x);

            // Anchor1：左→右到 Place2；右→左到 Place1
            if (anchor1 != null)
            {
                float a1 = anchor1.position.x;
                if (prevX < a1 && x >= a1)
                    MoveCameraTo(place2);
                else if (prevX > a1 && x <= a1)
                    MoveCameraTo(place1);
            }

            // Anchor2：左→右到 Place3；右→左到 Place2
            if (anchor2 != null)
            {
                float a2 = anchor2.position.x;
                if (prevX < a2 && x >= a2)
                    MoveCameraTo(place3);
                else if (prevX > a2 && x <= a2)
                    MoveCameraTo(place2);
            }
        }

        private void ResolveReferences()
        {
            if (place1 == null)
                place1 = transform.Find("Place1");
            if (place2 == null)
                place2 = transform.Find("Place2");
            if (place3 == null)
                place3 = transform.Find("Place3");
            if (anchor1 == null)
                anchor1 = transform.Find("Anchor1");
            if (anchor2 == null)
                anchor2 = transform.Find("Anchor2");

            if (place1 == null || place2 == null || place3 == null || anchor1 == null || anchor2 == null)
            {
                Debug.LogWarning(
                    "[Level1AnchorTriggers] 缺少 Place1/2/3 或 Anchor1/2，请检查子物体或 Inspector 引用。",
                    this);
            }
        }

        private bool TryGetPlayer(out Transform playerTransform)
        {
            playerTransform = null;
            if (player == null)
                player = FindObjectOfType<PlayerController>();

            if (player == null)
                return false;

            playerTransform = player.transform;
            return true;
        }

        /// <summary>本关第一次捡到任意颜色花时播放 Story2。</summary>
        private void TryTriggerFirstFlowerStory()
        {
            if (!playStoryOnFirstFlowerPickup || hasPlayedFirstFlowerStory)
                return;

            if (!TryGetPlayerInteractor(out PlayerInteractor interactor))
                return;

            if (interactor.HeldItem is not InteractableObjectFlower)
                return;

            hasPlayedFirstFlowerStory = true;
            PlayStoryNow(ResolveFirstFlowerStory(), "首次摘花剧情");
        }

        /// <summary>
        /// 本关在里世界第一次到达 Place2 的 X 时播放 Story4。
        /// 表世界越过不算；若已在 Place2 以右再切到里世界，也会触发一次。
        /// </summary>
        private void TryTriggerPlace2Story(float prevX, float x)
        {
            if (!CanPlayPlace2Story())
                return;

            float place2X = place2.position.x;
            if (!(prevX < place2X && x >= place2X))
                return;

            PlayPlace2Story();
        }

        private void OnActiveWorldChanged(WorldId world)
        {
            if (world != WorldId.B)
                return;

            // 已站在 Place2 以右时切到里世界，也算第一次到达。
            if (!CanPlayPlace2Story())
                return;

            if (!TryGetPlayer(out Transform playerTransform))
                return;

            if (playerTransform.position.x < place2.position.x)
                return;

            PlayPlace2Story();
        }

        private bool CanPlayPlace2Story()
        {
            if (!playStoryOnFirstReachPlace2 || hasPlayedPlace2Story || place2 == null)
                return false;

            return GameManager.Instance != null && GameManager.Instance.IsInUnderworld;
        }

        private void PlayPlace2Story()
        {
            hasPlayedPlace2Story = true;
            PlayStoryNow(ResolvePlace2Story(), "Place2里世界剧情");
        }

        private bool TryGetPlayerInteractor(out PlayerInteractor interactor)
        {
            interactor = playerInteractor;
            if (interactor != null)
                return true;

            if (player != null)
                interactor = player.GetComponent<PlayerInteractor>();

            if (interactor == null)
                interactor = FindObjectOfType<PlayerInteractor>();

            playerInteractor = interactor;
            return interactor != null;
        }

        private void OnLightsOffChanged(RoomLightsInfo _)
        {
            EvaluateLightsAndClearStories();
        }

        /// <summary>红黄花交付完成后，将 flowersDelivered 置为 true（不再由收集器直接切关）。</summary>
        private void TrySyncFlowerDeliveryCondition()
        {
            if (flowersDelivered)
                return;

            TryBindFlowerCollector();

            if (flowerCollector != null && flowerCollector.IsCompleted)
                flowersDelivered = true;
        }

        /// <summary>本关第一次成功交付红花或黄花时播放 Story5（只一次）。</summary>
        private void TryTriggerFirstFlowerDeliveryStory()
        {
            if (!playStoryOnFirstFlowerDelivery || hasPlayedFirstFlowerDeliveryStory)
                return;

            TryBindFlowerCollector();

            if (flowerCollector == null)
                return;

            if (flowerCollector.CollectedRed <= 0 && flowerCollector.CollectedYellow <= 0)
                return;

            hasPlayedFirstFlowerDeliveryStory = true;
            PlayStoryNow(ResolveFirstFlowerDeliveryStory(), "首次交付花朵剧情");
        }

        private void TryBindFlowerCollector()
        {
            if (flowerCollectorBound && flowerCollector != null)
                return;

            if (flowerCollector == null)
                flowerCollector = FindObjectOfType<InteractableObjectFlowerCollector>();

            if (flowerCollector == null)
                return;

            flowerCollector.WrongFlowerDeliveryAttempted -= OnWrongFlowerDeliveryAttempted;
            flowerCollector.WrongFlowerDeliveryAttempted += OnWrongFlowerDeliveryAttempted;
            flowerCollectorBound = true;
        }

        private void UnbindFlowerCollector()
        {
            if (flowerCollector != null)
                flowerCollector.WrongFlowerDeliveryAttempted -= OnWrongFlowerDeliveryAttempted;

            flowerCollectorBound = false;
        }

        /// <summary>错误交花（非红非黄）时播放 Story6（每次都触发）。</summary>
        private void OnWrongFlowerDeliveryAttempted()
        {
            if (!sceneReady || !playStoryOnWrongFlowerDelivery)
                return;

            PlayStoryNow(ResolveWrongFlowerDeliveryStory(), "错误交付花朵剧情");
        }

        /// <summary>
        /// 里世界与鬼魂互动：第一次播 Story7；第二次及以后每次播 Story8。
        /// </summary>
        private void OnGhostInteracted()
        {
            if (!sceneReady)
                return;

            if (GameManager.Instance == null || !GameManager.Instance.IsInUnderworld)
                return;

            underworldGhostInteractCount++;

            if (underworldGhostInteractCount == 1)
            {
                if (playStoryOnFirstGhostInteract)
                    PlayStoryNow(ResolveFirstGhostInteractStory(), "首次与鬼魂互动剧情");
                return;
            }

            if (playStoryOnRepeatGhostInteract)
                PlayStoryNow(ResolveRepeatGhostInteractStory(), "再次与鬼魂互动剧情");
        }

        /// <summary>
        /// 灯光目标：卧室亮、客厅暗、厨房亮（room_1 / room_2 / room_3）。
        /// 未写入缓存时按初始态：卧室亮、客厅暗、厨房暗。
        /// </summary>
        private bool IsLightsConditionMet()
        {
            return IsRoomLightsOn(bedroomRoomId, requireBedroomLightsOn, defaultLightsOn: true)
                && IsRoomLightsOn(livingRoomId, requireLivingRoomLightsOn, defaultLightsOn: false)
                && IsRoomLightsOn(kitchenRoomId, requireKitchenLightsOn, defaultLightsOn: false);
        }

        private static bool IsRoomLightsOn(string roomId, bool requireOn, bool defaultLightsOn)
        {
            bool lightsOn = defaultLightsOn;
            if (RoomLightsState.TryGet(roomId, out bool lightsOff))
                lightsOn = !lightsOff;

            return lightsOn == requireOn;
        }

        /// <summary>
        /// 灯光首次正确：播 Story9；若此时花也完成则接着播 Story10 并切关。
        /// 花后补完成：只播 Story10 再切关。
        /// </summary>
        private void EvaluateLightsAndClearStories()
        {
            if (!sceneReady || clearSequenceRunning || levelAdvanceStarted)
                return;

            TrySyncFlowerDeliveryCondition();
            bool lightsOk = IsLightsConditionMet();
            if (!lightsOk)
                return;

            if (!hasPlayedLightsCorrectStory)
            {
                hasPlayedLightsCorrectStory = true;
                if (flowersDelivered)
                {
                    levelAdvanceStarted = true;
                    clearSequenceRoutine = StartCoroutine(
                        PlayClearSequenceRoutine(playStory9: true, playStory10: true, thenAdvance: true));
                }
                else
                {
                    clearSequenceRoutine = StartCoroutine(
                        PlayClearSequenceRoutine(playStory9: true, playStory10: false, thenAdvance: false));
                }

                return;
            }

            if (!flowersDelivered)
                return;

            levelAdvanceStarted = true;
            clearSequenceRoutine = StartCoroutine(
                PlayClearSequenceRoutine(playStory9: false, playStory10: true, thenAdvance: true));
        }

        private IEnumerator PlayClearSequenceRoutine(bool playStory9, bool playStory10, bool thenAdvance)
        {
            clearSequenceRunning = true;

            if (playStory9 && playStoryOnLightsCorrect)
                yield return PlayStoryAndWait(ResolveLightsCorrectStory(), "灯光正确剧情");

            if (playStory10 && playStoryOnLevelClear)
                yield return PlayStoryAndWait(ResolveLevelClearStory(), "通关剧情");

            clearSequenceRunning = false;
            clearSequenceRoutine = null;

            if (!thenAdvance)
                yield break;

            // Story10 播完、切关前：关掉 ESC，并关闭已打开的暂停菜单。
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ClosePauseMenuIfOpen();
                GameManager.Instance.SetPauseMenuEscEnabled(false);
            }

            LevelManager manager = LevelManager.EnsureExists();
            if (!manager.CompleteAndLoadNextLevel())
            {
                Debug.LogWarning(
                    "[Level1AnchorTriggers] 通关条件已满足，但没有下一关可加载。",
                    this);
                levelAdvanceStarted = false;
                if (GameManager.Instance != null)
                    GameManager.Instance.SetPauseMenuEscEnabled(true);
            }
        }

        private IEnumerator PlayStoryAndWait(StorySequence story, string storyLabel)
        {
            if (story == null || !story.HasLines)
            {
                Debug.LogWarning($"[Level1AnchorTriggers] {storyLabel}配置缺失或为空。", this);
                yield break;
            }

            PlayStoryNow(story, storyLabel);
            yield return null;

            CameraTopBannerUI banner = CameraTopBannerUI.Instance;
            while (banner != null && banner.IsPlayingStory)
                yield return null;
        }

        private void SnapCameraTo(Transform place)
        {
            if (place == null)
                return;

            Camera cam = GetCamera();
            if (cam == null)
                return;

            Vector3 p = cam.transform.position;
            p.x = place.position.x;
            p.y = place.position.y + cameraYOffset;
            cam.transform.position = p;
            currentPlace = place;

            if (CameraManager.Instance != null)
                CameraManager.Instance.SnapTo(p);
        }

        private void MoveCameraTo(Transform place)
        {
            if (place == null || place == currentPlace)
                return;

            currentPlace = place;

            if (CameraManager.Instance != null)
                CameraManager.Instance.SetFollowTarget(null);

            if (moveRoutine != null)
                StopCoroutine(moveRoutine);

            moveRoutine = StartCoroutine(MoveCameraRoutine(place));
        }

        private IEnumerator MoveCameraRoutine(Transform place)
        {
            Camera cam = GetCamera();
            if (cam == null || place == null)
            {
                moveRoutine = null;
                yield break;
            }

            Vector3 start = cam.transform.position;
            Vector3 end = new(
                place.position.x,
                place.position.y + cameraYOffset,
                start.z);

            float duration = Mathf.Max(0.01f, cameraMoveDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = t * t * (3f - 2f * t); // smoothstep
                cam.transform.position = Vector3.Lerp(start, end, s);
                yield return null;
            }

            cam.transform.position = end;
            moveRoutine = null;
        }

        private static Camera GetCamera()
        {
            if (CameraManager.Instance != null && CameraManager.Instance.Camera != null)
                return CameraManager.Instance.Camera;
            return Camera.main;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawPlaceGizmo(place1 != null ? place1 : transform.Find("Place1"), Color.cyan);
            DrawPlaceGizmo(place2 != null ? place2 : transform.Find("Place2"), Color.green);
            DrawPlaceGizmo(place3 != null ? place3 : transform.Find("Place3"), Color.yellow);
            DrawAnchorLine(anchor1 != null ? anchor1 : transform.Find("Anchor1"), Color.magenta);
            DrawAnchorLine(anchor2 != null ? anchor2 : transform.Find("Anchor2"), Color.red);
        }

        private static void DrawPlaceGizmo(Transform t, Color color)
        {
            if (t == null)
                return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(t.position, 0.4f);
        }

        private static void DrawAnchorLine(Transform t, Color color)
        {
            if (t == null)
                return;
            Gizmos.color = color;
            Vector3 p = t.position;
            Gizmos.DrawLine(p + Vector3.up * 5f, p + Vector3.down * 5f);
        }
#endif
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BokeGameJam.Input;
using BokeGameJam.Levels;
using BokeGameJam.UI;

namespace BokeGameJam.Core
{
    /// <summary>
    /// 全局游戏流程编排器（单例，跨场景不销毁）。
    ///
    /// 职责边界：
    /// - 不负责 <b>场景切换</b>（那是 <see cref="GameSceneManager"/>）。
    /// - 不负责 <b>关卡数据</b>（那是 <c>LevelManager</c>）。
    /// - 不负责 <b>UI 预制体实例化</b>（那是 <see cref="UIManager"/>）。
    /// - 负责：维护当前 <see cref="GameState"/>，并在状态迁移时决定 <i>加载/隐藏/关闭哪些 UI</i>。
    /// - 负责：维护当前 <see cref="WorldId"/>（阳间/阴间），订阅 Shift 并广播 <see cref="GameEvents.ActiveWorldChanged"/>。
    ///
    /// 状态迁移（由 <see cref="EventManager"/> 事件驱动）：
    /// <code>
    ///  Boot ─┐
    ///        ├─(MainMenu 场景)─▶ MainMenu
    ///        ├─(GameStartRequested / LevelSelected)─▶ LevelLoading
    ///        ├─(LevelStarted) ─▶ LevelPlaying    → 加载 InventorySlot HUD
    ///        ├─(LevelCompleted)▶ LevelCompleted  → 只广播事件（暂无结算 UI）
    ///        └─(回主菜单)     ─▶ MainMenu        → 卸载 HUD
    /// </code>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Runtime Scene → State 映射")]
        [Tooltip("匹配这些场景名的场景加载后，会自动进入 MainMenu 状态。")]
        [SerializeField]
        private List<string> mainMenuSceneNames = new() { "StartScene", "MainMenu", "LevelSelect" };

        [Header("Start Game")]
        [Tooltip("主菜单点开始时进入的关卡 id（写入 LevelSelection）。")]
        [SerializeField] private string startLevelId = "level_1";

        [Tooltip("主菜单点开始时加载的场景名（需与 Build Settings 一致）。")]
        [SerializeField] private string startSceneName = "Level1";

        [Header("Level Playing UI")]
        [Tooltip("进入 LevelPlaying 时通过 UIManager 加载的 UI resourceId 列表。")]
        [SerializeField] private List<string> levelPlayingUiIds = new() { "InventorySlot", "CameraTopBanner" };

        [Tooltip("离开关卡（回主菜单等）时关闭的 UI resourceId 列表。")]
        [SerializeField] private List<string> uiToCloseOnLevelExit = new() { "InventorySlot", "CameraTopBanner" };

        [Header("World")]
        [Tooltip("开局默认世界：A = 阳间，B = 阴间。")]
        [SerializeField] private WorldId startWorld = WorldId.A;

        [Header("Options")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool logStateChanges = true;

        [Header("Runtime (只读)")]
        [Tooltip("当前游戏流程状态，运行时由事件驱动更新。")]
        [SerializeField] private GameState state = GameState.Boot;

        private WorldId activeWorld;

        public GameState State => state;
        public WorldId ActiveWorld => activeWorld;

        /// <summary>当前是否在里世界（阴间 / World B）。</summary>
        public bool IsInUnderworld => activeWorld == WorldId.B;

        /// <summary>确保单例存在（若场景中没有则新建一个）。</summary>
        public static GameManager EnsureExists()
        {
            if (Instance != null) return Instance;
            GameObject go = new(nameof(GameManager));
            return go.AddComponent<GameManager>();
        }

        // ---------- 生命周期 ----------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            activeWorld = startWorld;
        }

        private void OnEnable()
        {
            EventManager.On(GameEvents.GameStartRequested, OnGameStartRequested);
            EventManager.On<LevelSelection>(GameEvents.LevelSelected, OnLevelSelected);
            EventManager.On<string>(GameEvents.LevelStarted, OnLevelStarted);
            EventManager.On<string>(GameEvents.LevelCompleted, OnLevelCompleted);
            EventManager.On(InputEvents.WorldToggle, OnWorldToggle);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EventManager.Off(GameEvents.GameStartRequested, OnGameStartRequested);
            EventManager.Off<LevelSelection>(GameEvents.LevelSelected, OnLevelSelected);
            EventManager.Off<string>(GameEvents.LevelStarted, OnLevelStarted);
            EventManager.Off<string>(GameEvents.LevelCompleted, OnLevelCompleted);
            EventManager.Off(InputEvents.WorldToggle, OnWorldToggle);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            // 首次启动：按当前活动场景判断初始状态
            EvaluateInitialState(SceneManager.GetActiveScene());
            // 确保订阅方在 Start 后能收到初始世界
            EventManager.Emit(GameEvents.ActiveWorldChanged, activeWorld);
        }

        // ---------- 事件处理 ----------

        private void OnWorldToggle()
        {
            ToggleWorld();

            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlayRandomSFXByResourcePaths(
                    1f,
                    GameSfxPaths.WorldSwitch2,
                    GameSfxPaths.WorldSwitch4);
            }
        }

        /// <summary>切换阳间 / 阴间。</summary>
        public void ToggleWorld()
        {
            SetWorld(activeWorld == WorldId.A ? WorldId.B : WorldId.A);
        }

        public void SetWorld(WorldId world)
        {
            if (activeWorld == world)
                return;

            activeWorld = world;
            EventManager.Emit(GameEvents.ActiveWorldChanged, activeWorld);
        }

        /// <summary>
        /// 还原为表世界（阳间）。关卡结束黑屏回调 / 回主菜单时调用，
        /// 避免切关或回菜单时仍停在里世界造成突兀。
        /// </summary>
        public void ResetToLivingWorld()
        {
            SetWorld(WorldId.A);
        }

        /// <summary>主菜单点开始：确保 LevelManager 在场，再广播 LevelSelected 进入正式开局流。</summary>
        private void OnGameStartRequested()
        {
            if (string.IsNullOrWhiteSpace(startSceneName))
            {
                Debug.LogError("[GameManager] startSceneName 为空，无法开始游戏。", this);
                return;
            }

            LevelManager.EnsureExists();

            string levelId = string.IsNullOrWhiteSpace(startLevelId) ? startSceneName : startLevelId.Trim();
            LevelSelection selection = new(levelId, 0, startSceneName.Trim(), string.Empty);
            EventManager.Emit(GameEvents.LevelSelected, selection);
        }

        private void OnLevelSelected(LevelSelection _)
        {
            ChangeState(GameState.LevelLoading);
        }

        private void OnLevelStarted(string _)
        {
            ChangeState(GameState.LevelPlaying);
            // 即使状态未变（例如异常路径），进关也强制打开 ESC，避免上一关通关关掉后进 Level2 仍不可用。
            SetEscPauseEnabled(true);
        }

        private void OnLevelCompleted(string _)
        {
            ChangeState(GameState.LevelCompleted);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;

            // 场景切回主菜单/选关：回到 MainMenu 状态
            if (IsMainMenuScene(scene.name))
            {
                ChangeState(GameState.MainMenu);
            }
            // 关卡场景加载：等 LevelManager 广播 LevelStarted 再切到 LevelPlaying。
            // 这里不主动动，避免和 LevelManager 竞态。
        }

        // ---------- 状态迁移 ----------

        /// <summary>外部（例如通关按钮 / GameOver）也可以直接调用来推进状态。</summary>
        public void ChangeState(GameState next)
        {
            if (next == state) return;

            GameState prev = state;
            state = next;

            if (logStateChanges)
                Debug.Log($"[GameManager] {prev} → {next}");

            HandleStateTransition(prev, next);
            EventManager.Emit(GameEvents.GameStateChanged, next);
        }

        private void HandleStateTransition(GameState prev, GameState next)
        {
            // 离开 LevelPlaying：禁用 ESC 暂停。
            // LevelCompleted 时保留 CameraTopBanner，供通关剧情（如 Story17）播放；
            // 回主菜单等路径仍会在下方 switch 里关闭关卡 HUD。
            if (prev == GameState.LevelPlaying && next != GameState.LevelPlaying)
            {
                SetEscPauseEnabled(false);
                if (next != GameState.LevelCompleted)
                    CloseLevelPlayingUIs();
            }

            switch (next)
            {
                case GameState.MainMenu:
                    SetEscPauseEnabled(false);
                    CloseLevelPlayingUIs();
                    // 回主菜单时还原为表世界，避免下次进关仍停留在里世界。
                    ResetToLivingWorld();
                    break;

                case GameState.LevelPlaying:
                    // 关卡进行中允许玩家按 ESC 打开暂停菜单，并加载持有物 HUD
                    SetEscPauseEnabled(true);
                    LoadLevelPlayingUIs();
                    break;
            }
        }

        private static void LoadLevelPlayingUIs()
        {
            if (Instance == null || UIManager.Instance == null)
            {
                Debug.LogWarning("[GameManager] UIManager missing, cannot load level playing UIs.");
                return;
            }

            List<string> ids = Instance.levelPlayingUiIds;
            if (ids == null)
                return;

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                UIManager.Instance.Load(id.Trim());
            }
        }

        private static void CloseLevelPlayingUIs()
        {
            if (Instance == null || UIManager.Instance == null)
                return;

            List<string> ids = Instance.uiToCloseOnLevelExit;
            if (ids == null)
                return;

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                UIManager.Instance.Close(id.Trim());
            }
        }

        /// <summary>切换挂在 UIManager 上的 <see cref="PauseMenuTrigger"/> 的 ESC 触发开关。</summary>
        public void SetPauseMenuEscEnabled(bool enabled)
        {
            SetEscPauseEnabled(enabled);
        }

        /// <summary>若暂停菜单已打开则关闭。</summary>
        public void ClosePauseMenuIfOpen()
        {
            if (UIManager.Instance == null)
                return;

            PauseMenuTrigger trigger = UIManager.Instance.GetComponent<PauseMenuTrigger>();
            if (trigger == null)
                trigger = UIManager.Instance.GetComponentInChildren<PauseMenuTrigger>(true);

            if (trigger != null)
                trigger.ClosePauseMenu();
            else if (UIManager.Instance.IsVisible(PauseMenuController.ResourceId))
                UIManager.Instance.Close(PauseMenuController.ResourceId);
        }

        private static void SetEscPauseEnabled(bool enabled)
        {
            if (UIManager.Instance == null)
                return;

            PauseMenuTrigger trigger = UIManager.Instance.GetComponent<PauseMenuTrigger>();
            if (trigger == null)
                trigger = UIManager.Instance.GetComponentInChildren<PauseMenuTrigger>(true);

            if (trigger == null)
            {
                Debug.LogWarning("[GameManager] 没找到 PauseMenuTrigger，无法切换 EscEnabled。请把它挂到 UIManager 预制体上。");
                return;
            }

            trigger.EscEnabled = enabled;
        }

        // ---------- 工具方法 ----------

        private void EvaluateInitialState(Scene scene)
        {
            if (IsMainMenuScene(scene.name))
                ChangeState(GameState.MainMenu);
            // 其他情况保持 Boot，等待事件驱动。
        }

        private bool IsMainMenuScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || mainMenuSceneNames == null)
                return false;

            foreach (string name in mainMenuSceneNames)
            {
                if (string.Equals(name, sceneName, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

    }
}

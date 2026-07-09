using UnityEngine;
using UnityEngine.SceneManagement;
using BokeGameJam.Core;

namespace BokeGameJam.Levels
{
    /// <summary>
    /// 关卡运行时会话（单例，跨场景不销毁）。
    /// - 监听 <see cref="GameEvents.LevelSelected"/>，记录"当前关卡"
    /// - 关卡加载完成时广播 <see cref="GameEvents.LevelStarted"/>
    /// - 提供 <see cref="CompleteCurrentLevel"/>：解锁下一关 + 广播 <see cref="GameEvents.LevelCompleted"/>
    ///
    /// 关卡内的脚本（例如 LevelLoader）只需读 <see cref="CurrentLevel"/> 就能知道要加载哪张地图。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Catalog")]
        [Tooltip("留空则运行时按名字从 Resources 加载：Resources/Levels/LevelCatalog")]
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private string catalogResourcePath = "Levels/LevelCatalog";

        [Header("Behavior")]
        [Tooltip("加载下一关时是否自动切场景（否则只广播事件）")]
        [SerializeField] private bool autoLoadSceneOnSelect = true;

        [SerializeField] private bool dontDestroyOnLoad = true;

        private LevelSelection currentSelection;
        private bool hasCurrent;

        public LevelCatalog Catalog => catalog ??= Resources.Load<LevelCatalog>(catalogResourcePath);

        /// <summary>当前正在游玩的关卡（尚未选任何关卡时 <see cref="HasCurrentLevel"/>=false）。</summary>
        public LevelSelection CurrentLevel => currentSelection;
        public bool HasCurrentLevel => hasCurrent;

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
        }

        private void OnEnable()
        {
            EventManager.On<LevelSelection>(GameEvents.LevelSelected, OnLevelSelected);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EventManager.Off<LevelSelection>(GameEvents.LevelSelected, OnLevelSelected);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---------- 事件回调 ----------

        private void OnLevelSelected(LevelSelection selection)
        {
            currentSelection = selection;
            hasCurrent = true;

            if (!autoLoadSceneOnSelect || string.IsNullOrEmpty(selection.SceneName))
                return;

            SceneManager.LoadScene(selection.SceneName);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!hasCurrent) return;
            if (mode != LoadSceneMode.Single) return;
            if (!string.IsNullOrEmpty(currentSelection.SceneName) && scene.name != currentSelection.SceneName)
                return;

            EventManager.Emit(GameEvents.LevelStarted, currentSelection.LevelId);
        }

        // ---------- 关卡进度 ----------

        /// <summary>通关当前关卡：解锁下一关 + 广播 LevelCompleted。</summary>
        public void CompleteCurrentLevel()
        {
            if (!hasCurrent) return;
            EventManager.Emit(GameEvents.LevelCompleted, currentSelection.LevelId);

            LevelCatalog c = Catalog;
            if (c == null) return;

            int idx = c.IndexOf(currentSelection.LevelId);
            if (idx < 0 || idx + 1 >= c.Count) return;

            LevelCatalog.Level next = c.Get(idx + 1);
            if (next != null)
                c.SetUnlocked(next.levelId, true);
        }

        /// <summary>直接以 levelId 触发一次选关（等价于点击选关按钮）。</summary>
        public void LoadLevelById(string levelId)
        {
            LevelCatalog c = Catalog;
            if (c == null) return;

            int idx = c.IndexOf(levelId);
            LevelCatalog.Level level = c.Get(idx);
            if (level == null)
            {
                Debug.LogError($"[LevelManager] 找不到关卡: {levelId}");
                return;
            }

            LevelSelection sel = new(level.levelId, idx, level.sceneName, level.levelFile);
            EventManager.Emit(GameEvents.LevelSelected, sel);
        }

        /// <summary>确保存在单例（若场景中没有则自动创建）。</summary>
        public static LevelManager EnsureExists()
        {
            if (Instance != null) return Instance;
            GameObject go = new(nameof(LevelManager));
            return go.AddComponent<LevelManager>();
        }
    }
}

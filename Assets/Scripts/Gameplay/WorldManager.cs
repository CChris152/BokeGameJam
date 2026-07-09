using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Input;

namespace BokeGameJam.Gameplay
{
    public enum WorldId
    {
        A = 0,
        B = 1
    }

    /// <summary>
    /// 双世界状态单例：订阅 Shift 切换，广播 ActiveWorldChanged。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private WorldId startWorld = WorldId.A;

        private WorldId activeWorld;

        public WorldId ActiveWorld => activeWorld;

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
            EventManager.On(InputEvents.WorldToggle, OnWorldToggle);
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.WorldToggle, OnWorldToggle);
        }

        private void Start()
        {
            // 确保订阅方（如 LevelEditor）在 Start 后能收到初始世界
            EventManager.Emit(GameEvents.ActiveWorldChanged, activeWorld);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnWorldToggle() => Toggle();

        public void Toggle()
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

        /// <summary>确保场景中存在 WorldManager 单例。</summary>
        public static WorldManager EnsureExists()
        {
            if (Instance != null)
                return Instance;

            GameObject go = new(nameof(WorldManager));
            return go.AddComponent<WorldManager>();
        }
    }
}

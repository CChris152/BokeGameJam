using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Input
{
    /// <summary>
    /// 全局输入总线：唯一读取 <see cref="UnityEngine.Input"/> 的入口，
    /// 根据当前 <see cref="InputContext"/> 通过 <see cref="EventManager"/> 广播语义化事件。
    ///
    /// 订阅方（Player / Camera / LevelEditor 等）不再直接读 Input，只监听 InputEvents。
    /// 挂在场景中的单例对象上（可用 DontDestroyOnLoad）。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Startup")]
        [SerializeField] private InputContext startContext = InputContext.Gameplay;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Keys - Player")]
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("Keys - Editor")]
        [SerializeField] private KeyCode toggleEditorKey = KeyCode.M;
        [SerializeField] private KeyCode saveKey = KeyCode.S;
        [SerializeField] private KeyCode loadKey = KeyCode.L;
        [SerializeField] private KeyCode clearKey = KeyCode.N;
        [Tooltip("保存/加载/清空需要按住的修饰键")]
        [SerializeField] private KeyCode editorModifierKey = KeyCode.LeftControl;

        [Header("Keys - Camera (Editor)")]
        [SerializeField] private KeyCode camLeftKey = KeyCode.A;
        [SerializeField] private KeyCode camRightKey = KeyCode.D;
        [SerializeField] private KeyCode camUpKey = KeyCode.W;
        [SerializeField] private KeyCode camDownKey = KeyCode.S;

        [Header("Keys - World")]
        [SerializeField] private KeyCode worldToggleKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode worldToggleAltKey = KeyCode.RightShift;

        private InputContext currentContext;

        public InputContext CurrentContext => currentContext;

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

            SetContext(startContext, forceEmit: true);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>切换输入上下文，并广播 ContextChanged 事件。</summary>
        public void SetContext(InputContext context, bool forceEmit = false)
        {
            if (!forceEmit && currentContext == context)
                return;

            currentContext = context;
            EventManager.Emit(InputEvents.ContextChanged, context);
        }

        private void Update()
        {
            // 编辑器切换键在所有上下文下都可用（除 UI 上下文外）
            if (currentContext != InputContext.UI && UnityEngine.Input.GetKeyDown(toggleEditorKey))
                EventManager.Emit(InputEvents.EditorToggle);

            switch (currentContext)
            {
                case InputContext.Gameplay:
                    PollGameplay();
                    break;
                case InputContext.LevelEditor:
                    PollLevelEditor();
                    break;
                case InputContext.UI:
                    // UI 上下文屏蔽游戏输入
                    break;
            }
        }

        // ---------- 游玩 ----------

        private void PollGameplay()
        {
            float horizontal = 0f;
            if (UnityEngine.Input.GetKey(leftKey) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
                horizontal -= 1f;
            if (UnityEngine.Input.GetKey(rightKey) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
                horizontal += 1f;

            EventManager.Emit(InputEvents.PlayerMove, Mathf.Clamp(horizontal, -1f, 1f));

            if (UnityEngine.Input.GetKeyDown(jumpKey))
                EventManager.Emit(InputEvents.PlayerJumpPressed);

            if (UnityEngine.Input.GetKeyDown(interactKey))
                EventManager.Emit(InputEvents.PlayerInteractPressed);

            PollWorldToggle();
        }

        // ---------- 关卡编辑器 ----------

        private void PollLevelEditor()
        {
            // 相机移动（WASD / 方向键）
            float x = 0f;
            float y = 0f;
            if (UnityEngine.Input.GetKey(camLeftKey) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (UnityEngine.Input.GetKey(camRightKey) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (UnityEngine.Input.GetKey(camDownKey) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (UnityEngine.Input.GetKey(camUpKey) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) y += 1f;

            Vector2 raw = new(x, y);
            Vector2 dir = raw.sqrMagnitude > 1f ? raw.normalized : raw;
            EventManager.Emit(InputEvents.CameraMove, dir);

            PollWorldToggle();

            // 滚轮缩放（仅编辑模式），unity 滚轮 delta.y：向上滚 = 正数（拉近）
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.0001f)
                EventManager.Emit(InputEvents.CameraZoom, scroll);

            // 鼠标绘制（每帧广播一次，订阅方自行决定是否在 UI 遮挡下忽略）
            if (UnityEngine.Input.GetMouseButton(0))
                EventManager.Emit(InputEvents.EditorPaintHeld);
            if (UnityEngine.Input.GetMouseButton(1))
                EventManager.Emit(InputEvents.EditorEraseHeld);

            // 数字键 1-9 选择调色板
            for (int i = 0; i < 9; i++)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i))
                    EventManager.Emit(InputEvents.EditorSelectPalette, i);
            }

            // Ctrl+S / Ctrl+L / Ctrl+N
            if (UnityEngine.Input.GetKey(editorModifierKey))
            {
                if (UnityEngine.Input.GetKeyDown(saveKey))
                    EventManager.Emit(InputEvents.EditorSave);
                else if (UnityEngine.Input.GetKeyDown(loadKey))
                    EventManager.Emit(InputEvents.EditorLoad);
                else if (UnityEngine.Input.GetKeyDown(clearKey))
                    EventManager.Emit(InputEvents.EditorClear);
            }
        }

        private void PollWorldToggle()
        {
            if (UnityEngine.Input.GetKeyDown(worldToggleKey) || UnityEngine.Input.GetKeyDown(worldToggleAltKey))
                EventManager.Emit(InputEvents.WorldToggle);
        }

        // ---------- 便捷方法 ----------

        /// <summary>确保场景中存在 InputManager 单例。</summary>
        public static InputManager EnsureExists()
        {
            if (Instance != null)
                return Instance;

            GameObject go = new(nameof(InputManager));
            return go.AddComponent<InputManager>();
        }
    }
}

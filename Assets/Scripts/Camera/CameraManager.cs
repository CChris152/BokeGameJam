using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Input;

namespace BokeGameJam.CameraSystem
{
    /// <summary>
    /// 全局相机管理器：
    ///   • Gameplay 上下文下平滑跟随目标（默认自动找 Player.tag=Player）
    ///   • LevelEditor 上下文下接收 InputEvents.CameraMove 事件，由 WASD 控制自由移动
    ///   • 通过 EventManager 与外部解耦，不直接引用 InputManager / PlayerController
    ///
    /// 使用：挂在 Main Camera 上（或独立空物体并指定 targetCamera）。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("跟随目标；留空则运行时按 Tag=Player 自动查找")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private string autoFindPlayerTag = "Player";

        [Header("Follow")]
        [SerializeField] private Vector2 followOffset = new(0f, 1.5f);
        [Tooltip("平滑跟随时间常数（越大越滞后，0 = 瞬移）")]
        [SerializeField] private float followSmoothTime = 0.15f;
        [Tooltip("死区：目标在该矩形内不触发跟随（相机局部坐标）")]
        [SerializeField] private Vector2 deadZone = new(0.5f, 0.5f);

        [Header("Bounds (optional)")]
        [SerializeField] private bool useBounds;
        [SerializeField] private Rect worldBounds = new(-50f, -20f, 100f, 40f);

        [Header("Editor Camera")]
        [Tooltip("编辑模式下 WASD 相机速度（单位/秒）")]
        [SerializeField] private float editorMoveSpeed = 8f;

        [Header("Editor Zoom")]
        [Tooltip("滚轮一格改变的正交尺寸（正交相机）")]
        [SerializeField] private float zoomStepOrtho = 1.0f;
        [Tooltip("滚轮一格改变的视野角度（透视相机）")]
        [SerializeField] private float zoomStepPerspective = 3.0f;
        [SerializeField] private float minOrthoSize = 2f;
        [SerializeField] private float maxOrthoSize = 40f;
        [SerializeField] private float minFov = 20f;
        [SerializeField] private float maxFov = 90f;
        [Tooltip("离开编辑模式时恢复到进入前的相机尺寸")]
        [SerializeField] private bool restoreZoomOnExit = true;

        private Vector3 followVelocity;
        private InputContext currentContext = InputContext.Gameplay;
        private Vector2 editorInputDir;
        /// <summary>为 true 时表示外部已显式清空跟随目标，禁止再按 Tag 自动找回玩家。</summary>
        private bool suppressAutoFindFollowTarget;

        // 记录进入编辑模式前的相机尺寸/FOV，退出时恢复
        private float savedOrthoSize;
        private float savedFov;
        private bool hasSavedZoom;

        public Camera Camera => targetCamera;
        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (targetCamera == null)
                targetCamera = GetComponent<Camera>() ?? Camera.main;
        }

        private void OnEnable()
        {
            EventManager.On<InputContext>(InputEvents.ContextChanged, OnContextChanged);
            EventManager.On<Vector2>(InputEvents.CameraMove, OnCameraMove);
            EventManager.On<float>(InputEvents.CameraZoom, OnCameraZoom);
        }

        private void OnDisable()
        {
            EventManager.Off<InputContext>(InputEvents.ContextChanged, OnContextChanged);
            EventManager.Off<Vector2>(InputEvents.CameraMove, OnCameraMove);
            EventManager.Off<float>(InputEvents.CameraZoom, OnCameraZoom);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (followTarget == null
                && !suppressAutoFindFollowTarget
                && !string.IsNullOrEmpty(autoFindPlayerTag))
                TryAutoFindPlayer();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                return;

            switch (currentContext)
            {
                case InputContext.Gameplay:
                case InputContext.UI:
                    if (followTarget == null
                        && !suppressAutoFindFollowTarget
                        && !string.IsNullOrEmpty(autoFindPlayerTag))
                        TryAutoFindPlayer();
                    FollowStep();
                    break;

                case InputContext.LevelEditor:
                    EditorMoveStep();
                    break;
            }

            if (useBounds)
                ClampToBounds();
        }

        // ---------- 跟随 ----------

        private void FollowStep()
        {
            if (followTarget == null)
                return;

            Vector3 camPos = targetCamera.transform.position;
            Vector3 desired = new(
                followTarget.position.x + followOffset.x,
                followTarget.position.y + followOffset.y,
                camPos.z);

            // 死区：只在超出死区的方向上移动
            Vector2 diff = new(desired.x - camPos.x, desired.y - camPos.y);
            Vector2 half = deadZone * 0.5f;

            if (Mathf.Abs(diff.x) < half.x) desired.x = camPos.x;
            else desired.x -= Mathf.Sign(diff.x) * half.x;

            if (Mathf.Abs(diff.y) < half.y) desired.y = camPos.y;
            else desired.y -= Mathf.Sign(diff.y) * half.y;

            if (followSmoothTime <= 0f)
                targetCamera.transform.position = desired;
            else
                targetCamera.transform.position = Vector3.SmoothDamp(camPos, desired, ref followVelocity, followSmoothTime);
        }

        private void TryAutoFindPlayer()
        {
            GameObject player = GameObject.FindWithTag(autoFindPlayerTag);
            if (player != null)
                followTarget = player.transform;
        }

        // ---------- 编辑模式相机 ----------

        private void EditorMoveStep()
        {
            if (editorInputDir.sqrMagnitude < 0.0001f)
                return;

            Vector3 delta = new Vector3(editorInputDir.x, editorInputDir.y, 0f) * (editorMoveSpeed * Time.unscaledDeltaTime);
            targetCamera.transform.position += delta;
        }

        private void ClampToBounds()
        {
            Vector3 p = targetCamera.transform.position;
            p.x = Mathf.Clamp(p.x, worldBounds.xMin, worldBounds.xMax);
            p.y = Mathf.Clamp(p.y, worldBounds.yMin, worldBounds.yMax);
            targetCamera.transform.position = p;
        }

        // ---------- 事件回调 ----------

        private void OnContextChanged(InputContext context)
        {
            InputContext prev = currentContext;
            currentContext = context;

            // 进入编辑模式：记录当前尺寸，稍后可以恢复
            if (prev != InputContext.LevelEditor && context == InputContext.LevelEditor && targetCamera != null)
            {
                savedOrthoSize = targetCamera.orthographicSize;
                savedFov = targetCamera.fieldOfView;
                hasSavedZoom = true;
            }

            // 离开编辑模式：可选恢复原缩放
            if (prev == InputContext.LevelEditor && context != InputContext.LevelEditor)
            {
                if (restoreZoomOnExit && hasSavedZoom && targetCamera != null)
                {
                    targetCamera.orthographicSize = savedOrthoSize;
                    targetCamera.fieldOfView = savedFov;
                }
                editorInputDir = Vector2.zero;
            }

            followVelocity = Vector3.zero;
        }

        private void OnCameraMove(Vector2 dir)
        {
            editorInputDir = dir;
        }

        private void OnCameraZoom(float scroll)
        {
            // 只在编辑模式下响应；游玩时滚轮留给别的用途（比如 UI）
            if (currentContext != InputContext.LevelEditor || targetCamera == null)
                return;

            if (targetCamera.orthographic)
            {
                float next = targetCamera.orthographicSize - scroll * zoomStepOrtho;
                targetCamera.orthographicSize = Mathf.Clamp(next, minOrthoSize, maxOrthoSize);
            }
            else
            {
                float next = targetCamera.fieldOfView - scroll * zoomStepPerspective;
                targetCamera.fieldOfView = Mathf.Clamp(next, minFov, maxFov);
            }
        }

        // ---------- 公开接口 ----------

        /// <summary>把相机对准目标位置（不受死区约束）。</summary>
        public void SnapTo(Vector3 worldPos)
        {
            if (targetCamera == null) return;
            Vector3 p = targetCamera.transform.position;
            p.x = worldPos.x;
            p.y = worldPos.y;
            targetCamera.transform.position = p;
            followVelocity = Vector3.zero;
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            // null = 明确关闭跟随；非 null = 恢复正常跟随（允许之后再自动查找）
            suppressAutoFindFollowTarget = target == null;
        }
    }
}

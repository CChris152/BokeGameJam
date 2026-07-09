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
        [Tooltip("编辑模式下 WASD 相机基础速度（单位/秒）")]
        [SerializeField] private float editorMoveSpeed = 8f;
        [Tooltip("Shift 加速倍率")]
        [SerializeField] private float editorBoostMultiplier = 3f;

        private Vector3 followVelocity;
        private InputContext currentContext = InputContext.Gameplay;
        private Vector2 editorInputDir;
        private bool editorBoost;

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
            EventManager.On<bool>(InputEvents.CameraBoost, OnCameraBoost);
        }

        private void OnDisable()
        {
            EventManager.Off<InputContext>(InputEvents.ContextChanged, OnContextChanged);
            EventManager.Off<Vector2>(InputEvents.CameraMove, OnCameraMove);
            EventManager.Off<bool>(InputEvents.CameraBoost, OnCameraBoost);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (followTarget == null && !string.IsNullOrEmpty(autoFindPlayerTag))
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
                    if (followTarget == null && !string.IsNullOrEmpty(autoFindPlayerTag))
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

            float speed = editorMoveSpeed * (editorBoost ? editorBoostMultiplier : 1f);
            Vector3 delta = new Vector3(editorInputDir.x, editorInputDir.y, 0f) * (speed * Time.unscaledDeltaTime);
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
            currentContext = context;
            // 切换到编辑模式时清零残留输入，避免持续滑动
            if (context != InputContext.LevelEditor)
            {
                editorInputDir = Vector2.zero;
                editorBoost = false;
            }
            followVelocity = Vector3.zero;
        }

        private void OnCameraMove(Vector2 dir)
        {
            editorInputDir = dir;
        }

        private void OnCameraBoost(bool boost)
        {
            editorBoost = boost;
        }

        // ---------- API ----------

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
        }
    }
}

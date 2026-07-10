using System.Collections;
using BokeGameJam.CameraSystem;
using BokeGameJam.Gameplay;
using BokeGameJam.Input;
using BokeGameJam.UI;
using UnityEngine;

namespace BokeGameJam.Puzzles.Mirror
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class MirrorPuzzleFrame : MonoBehaviour, IInteractable
    {
        [Header("Puzzle")]
        [SerializeField] private string puzzleId = "mirror_1";
        [SerializeField] private bool canRepeatAfterSolved;

        [Header("UI")]
        [SerializeField] private MirrorShardPuzzlePanel panelPrefab;
        [SerializeField] private Transform uiParentOverride;

        [Header("Focus")]
        [SerializeField] private bool animateCameraFocus;
        [SerializeField] private Transform focusTarget;
        [SerializeField] private float focusDuration = 0.35f;
        [SerializeField] private float focusedOrthoSize = 3.5f;
        [SerializeField] private AnimationCurve focusEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Completion")]
        [SerializeField] private bool emitSignalOnSolved = true;
        [SerializeField] private string solvedSignal = "solved";
        [SerializeField] private bool setStateOnSolved = true;
        [SerializeField] private string solvedState = "solved";
        [SerializeField] private bool closePanelWhenSolved = true;

        private MirrorShardPuzzlePanel activePanel;
        private InputContext previousContext = InputContext.Gameplay;
        private bool hasPreviousContext;
        private bool isOpen;
        private bool isSolved;

        private Camera focusedCamera;
        private Transform previousFollowTarget;
        private Vector3 previousCameraPosition;
        private float previousOrthoSize;
        private Coroutine focusRoutine;

        public Vector3 InteractionPosition => transform.position;
        public bool HasPanelPrefab => panelPrefab != null;
        public bool IsOpen => isOpen;
        public bool IsSolved => isSolved;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null && !trigger.isTrigger)
            {
                Debug.LogWarning(
                    $"[MirrorPuzzleFrame] '{name}' should usually use a trigger Collider2D for player interaction.",
                    this);
            }
        }

        private void OnDisable()
        {
            CleanupOpenState();
        }

        public void SetInInteractRange(bool inRange)
        {
            // Reserved for highlight/prompt visuals on the art prefab.
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return enabled
                && gameObject.activeInHierarchy
                && !isOpen
                && (canRepeatAfterSolved || !isSolved);
        }

        public bool Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return false;

            Open();
            return true;
        }

        public void Open()
        {
            if (isOpen)
                return;

            if (panelPrefab == null)
            {
                Debug.LogError("[MirrorPuzzleFrame] Missing MirrorShardPuzzlePanel prefab.", this);
                return;
            }

            isOpen = true;
            EnsureEventSystem();
            EnterUiContext();
            BeginCameraFocus();

            Transform parent = ResolveUiParent();
            activePanel = Instantiate(panelPrefab, parent, false);
            activePanel.name = string.IsNullOrWhiteSpace(puzzleId) ? "MirrorShardPuzzlePanel" : $"MirrorShardPuzzlePanel_{puzzleId.Trim()}";
            activePanel.Solved += OnPanelSolved;
            activePanel.Closed += OnPanelClosed;
            activePanel.Open(puzzleId);
        }

        private void OnPanelSolved()
        {
            isSolved = true;

            if (emitSignalOnSolved)
                PuzzleSignalHub.Emit(solvedSignal, gameObject, puzzleId);

            if (setStateOnSolved)
                PuzzleStateHub.SetState(puzzleId, solvedState, true, gameObject);

            if (closePanelWhenSolved && activePanel != null)
                activePanel.RequestClose();
        }

        private void OnPanelClosed()
        {
            if (activePanel != null)
            {
                activePanel.Solved -= OnPanelSolved;
                activePanel.Closed -= OnPanelClosed;
                Destroy(activePanel.gameObject);
                activePanel = null;
            }

            EndCameraFocus();
            ExitUiContext();
            isOpen = false;
        }

        private Transform ResolveUiParent()
        {
            if (uiParentOverride != null)
                return uiParentOverride;

            if (UIManager.Instance != null && UIManager.Instance.UIRoot != null)
                return UIManager.Instance.UIRoot;

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                return canvas.transform;

            GameObject canvasObject = new("MirrorPuzzleCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
            createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            createdCanvas.sortingOrder = 200;
            return canvasObject.transform;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            new GameObject(
                "EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        private void EnterUiContext()
        {
            InputManager input = InputManager.Instance;
            if (input == null)
                return;

            previousContext = input.CurrentContext;
            hasPreviousContext = true;
            input.SetContext(InputContext.UI);
        }

        private void ExitUiContext()
        {
            if (!hasPreviousContext || InputManager.Instance == null)
                return;

            InputManager.Instance.SetContext(previousContext);
            hasPreviousContext = false;
        }

        private void BeginCameraFocus()
        {
            if (!animateCameraFocus)
                return;

            CameraManager cameraManager = CameraManager.Instance;
            focusedCamera = cameraManager != null ? cameraManager.Camera : Camera.main;
            if (focusedCamera == null)
                return;

            previousCameraPosition = focusedCamera.transform.position;
            previousOrthoSize = focusedCamera.orthographicSize;

            Vector3 targetPosition = focusTarget != null ? focusTarget.position : transform.position;
            targetPosition.z = focusedCamera.transform.position.z;
            float targetOrtho = focusedCamera.orthographic ? Mathf.Max(0.1f, focusedOrthoSize) : focusedCamera.orthographicSize;

            if (cameraManager != null)
            {
                previousFollowTarget = cameraManager.FollowTarget;
                cameraManager.SetFollowTarget(focusTarget != null ? focusTarget : transform);
                cameraManager.SnapTo(targetPosition);
                StartFocusRoutine(targetPosition, targetOrtho, null, animatePosition: false);
                return;
            }

            StartFocusRoutine(targetPosition, targetOrtho);
        }

        private void EndCameraFocus()
        {
            if (!animateCameraFocus || focusedCamera == null)
                return;

            if (CameraManager.Instance != null)
            {
                RestoreFollowTarget();
                StartFocusRoutine(previousCameraPosition, previousOrthoSize, ClearFocusedCamera, animatePosition: false);
                return;
            }

            StartFocusRoutine(previousCameraPosition, previousOrthoSize, ClearFocusedCamera);
        }

        private void StartFocusRoutine(Vector3 targetPosition, float targetOrthoSize, System.Action onComplete = null, bool animatePosition = true)
        {
            if (focusRoutine != null)
                StopCoroutine(focusRoutine);

            focusRoutine = StartCoroutine(AnimateCamera(targetPosition, targetOrthoSize, onComplete, animatePosition));
        }

        private IEnumerator AnimateCamera(Vector3 targetPosition, float targetOrthoSize, System.Action onComplete, bool animatePosition)
        {
            if (focusedCamera == null)
                yield break;

            Vector3 startPosition = focusedCamera.transform.position;
            float startOrthoSize = focusedCamera.orthographicSize;
            float duration = Mathf.Max(0.01f, focusDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = focusEase != null ? focusEase.Evaluate(t) : t;

                if (animatePosition)
                    focusedCamera.transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);

                if (focusedCamera.orthographic)
                    focusedCamera.orthographicSize = Mathf.LerpUnclamped(startOrthoSize, targetOrthoSize, eased);

                yield return null;
            }

            if (animatePosition)
                focusedCamera.transform.position = targetPosition;

            if (focusedCamera.orthographic)
                focusedCamera.orthographicSize = targetOrthoSize;

            focusRoutine = null;
            onComplete?.Invoke();
        }

        private void RestoreFollowTarget()
        {
            if (CameraManager.Instance != null)
                CameraManager.Instance.SetFollowTarget(previousFollowTarget);
        }

        private void ClearFocusedCamera()
        {
            focusedCamera = null;
            previousFollowTarget = null;
        }

        private void CleanupOpenState()
        {
            if (!isOpen && activePanel == null && !hasPreviousContext && focusedCamera == null)
                return;

            if (activePanel != null)
            {
                activePanel.Solved -= OnPanelSolved;
                activePanel.Closed -= OnPanelClosed;
                Destroy(activePanel.gameObject);
                activePanel = null;
            }

            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
                focusRoutine = null;
            }

            if (focusedCamera != null)
            {
                if (CameraManager.Instance != null)
                    RestoreFollowTarget();

                focusedCamera.transform.position = previousCameraPosition;
                if (focusedCamera.orthographic)
                    focusedCamera.orthographicSize = previousOrthoSize;
                ClearFocusedCamera();
            }

            ExitUiContext();
            isOpen = false;
        }
    }
}

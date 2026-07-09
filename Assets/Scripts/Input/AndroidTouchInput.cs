using BokeGameJam.Core;
using BokeGameJam.UI;
using UnityEngine;

namespace BokeGameJam.Input
{
    /// <summary>
    /// Minimal Android touch controls that emit the same input events as InputManager.
    /// It bootstraps only on Android player builds and does not affect Windows or Editor play mode.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class AndroidTouchInput : MonoBehaviour
    {
        [SerializeField] private bool simulateInEditor;
        [SerializeField] private float minButtonSize = 72f;
        [SerializeField] private float maxButtonSize = 128f;
        [SerializeField] private float buttonSizeRatio = 0.14f;
        [SerializeField] private float gapRatio = 0.022f;
        [SerializeField] private float edgeRatio = 0.035f;

        private static AndroidTouchInput instance;

        private bool leftHeld;
        private bool rightHeld;
        private bool jumpQueued;
        private bool interactQueued;
        private bool worldToggleQueued;
        private bool pauseQueued;
        private bool emittedMoveLastFrame;

        private Rect leftRect;
        private Rect rightRect;
        private Rect jumpRect;
        private Rect interactRect;
        private Rect worldRect;
        private Rect pauseRect;

#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
                return;

            GameObject go = new(nameof(AndroidTouchInput));
            go.AddComponent<AndroidTouchInput>();
        }
#endif

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyAndroidOrientation();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (!IsEnabledForCurrentPlatform() || !IsGameplayContext())
            {
                EmitMoveStopIfNeeded();
                ResetFrameInput();
                return;
            }

            UpdateButtonRects();
            PollTouchInput();

            float horizontal = 0f;
            if (leftHeld)
                horizontal -= 1f;
            if (rightHeld)
                horizontal += 1f;

            EventManager.Emit(InputEvents.PlayerMove, Mathf.Clamp(horizontal, -1f, 1f));
            emittedMoveLastFrame = true;

            if (jumpQueued)
                EventManager.Emit(InputEvents.PlayerJumpPressed);
            if (interactQueued)
                EventManager.Emit(InputEvents.PlayerInteractPressed);
            if (worldToggleQueued)
                EventManager.Emit(InputEvents.WorldToggle);
            if (pauseQueued)
                TogglePauseMenu();

            jumpQueued = false;
            interactQueued = false;
            worldToggleQueued = false;
            pauseQueued = false;
        }

        private void OnGUI()
        {
            if (!IsEnabledForCurrentPlatform() || !IsGameplayContext())
                return;

            UpdateButtonRects();

            GUIStyle style = GetButtonStyle(leftRect.height);
            DrawButton(leftRect, "Left", leftHeld, style);
            DrawButton(rightRect, "Right", rightHeld, style);
            DrawButton(interactRect, "Use", false, style);
            DrawButton(jumpRect, "Jump", false, style);
            DrawButton(worldRect, "World", false, style);
            DrawButton(pauseRect, "Pause", false, style);
        }

        private bool IsEnabledForCurrentPlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return simulateInEditor;
#endif
        }

        private static void ApplyAndroidOrientation()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
        }

        private static bool IsGameplayContext()
        {
            if (InputManager.Instance != null && InputManager.Instance.CurrentContext != InputContext.Gameplay)
                return false;

            return GameManager.Instance == null || GameManager.Instance.State == GameState.LevelPlaying;
        }

        private void UpdateButtonRects()
        {
            Rect safe = Screen.safeArea;
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            float size = Mathf.Clamp(shortSide * buttonSizeRatio, minButtonSize, maxButtonSize);
            float gap = Mathf.Clamp(shortSide * gapRatio, 12f, 28f);
            float edge = Mathf.Clamp(shortSide * edgeRatio, 18f, 40f);

            float safeLeft = safe.xMin;
            float safeRight = safe.xMax;
            float safeTop = Screen.height - safe.yMax;
            float safeBottom = Screen.height - safe.yMin;
            float bottomY = safeBottom - edge - size;

            leftRect = new Rect(safeLeft + edge, bottomY, size, size);
            rightRect = new Rect(leftRect.xMax + gap, bottomY, size, size);

            jumpRect = new Rect(safeRight - edge - size, bottomY, size, size);
            interactRect = new Rect(jumpRect.xMin - gap - size, bottomY, size, size);
            worldRect = new Rect(jumpRect.xMin, bottomY - gap - size, size, size);
            pauseRect = new Rect(safeRight - edge - size, safeTop + edge, size, size * 0.68f);
        }

        private void PollTouchInput()
        {
            leftHeld = false;
            rightHeld = false;

            bool touched = false;
            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                Touch touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended)
                    continue;

                touched = true;
                Vector2 guiPosition = ToGuiPosition(touch.position);
                PollPointer(guiPosition, touch.phase == TouchPhase.Began);
            }

            if (!touched && simulateInEditor)
                PollMouseInput();
        }

        private void PollMouseInput()
        {
            if (!UnityEngine.Input.GetMouseButton(0))
                return;

            Vector2 guiPosition = ToGuiPosition(UnityEngine.Input.mousePosition);
            PollPointer(guiPosition, UnityEngine.Input.GetMouseButtonDown(0));
        }

        private void PollPointer(Vector2 guiPosition, bool began)
        {
            if (leftRect.Contains(guiPosition))
                leftHeld = true;
            if (rightRect.Contains(guiPosition))
                rightHeld = true;

            if (!began)
                return;

            if (jumpRect.Contains(guiPosition))
                jumpQueued = true;
            else if (interactRect.Contains(guiPosition))
                interactQueued = true;
            else if (worldRect.Contains(guiPosition))
                worldToggleQueued = true;
            else if (pauseRect.Contains(guiPosition))
                pauseQueued = true;
        }

        private static Vector2 ToGuiPosition(Vector2 screenPosition)
        {
            return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        }

        private void ResetFrameInput()
        {
            leftHeld = false;
            rightHeld = false;
            jumpQueued = false;
            interactQueued = false;
            worldToggleQueued = false;
            pauseQueued = false;
        }

        private void EmitMoveStopIfNeeded()
        {
            if (!emittedMoveLastFrame)
                return;

            EventManager.Emit(InputEvents.PlayerMove, 0f);
            emittedMoveLastFrame = false;
        }

        private static void DrawButton(Rect rect, string label, bool active, GUIStyle style)
        {
            Color previousColor = GUI.color;
            GUI.color = active ? new Color(0.45f, 0.85f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.72f);
            GUI.Box(rect, label, style);
            GUI.color = previousColor;
        }

        private static GUIStyle GetButtonStyle(float size)
        {
            GUIStyle style = new(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(size * 0.22f),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            return style;
        }

        private static void TogglePauseMenu()
        {
            PauseMenuTrigger trigger = Object.FindObjectOfType<PauseMenuTrigger>();
            if (trigger == null)
            {
                Debug.LogWarning("[AndroidTouchInput] PauseMenuTrigger is missing.");
                return;
            }

            trigger.TogglePauseMenu();
        }
    }
}

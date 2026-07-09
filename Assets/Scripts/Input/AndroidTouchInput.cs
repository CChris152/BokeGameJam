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
        [SerializeField] private float buttonSize = 88f;
        [SerializeField] private float buttonGap = 16f;
        [SerializeField] private float bottomMargin = 24f;

        private static AndroidTouchInput instance;

        private bool leftHeld;
        private bool rightHeld;
        private bool jumpQueued;
        private bool interactQueued;
        private bool worldToggleQueued;
        private bool pauseQueued;

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
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (!IsEnabledForCurrentPlatform() || !IsGameplayContext())
                return;

            float horizontal = 0f;
            if (leftHeld)
                horizontal -= 1f;
            if (rightHeld)
                horizontal += 1f;

            EventManager.Emit(InputEvents.PlayerMove, Mathf.Clamp(horizontal, -1f, 1f));

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

            float scale = GetUiScale();
            float size = buttonSize * scale;
            float gap = buttonGap * scale;
            float bottom = bottomMargin * scale;
            float y = Screen.height - bottom - size;

            GUIStyle style = GetButtonStyle(size);

            Rect leftRect = new(gap, y, size, size);
            Rect rightRect = new(gap + size + gap, y, size, size);
            Rect jumpRect = new(Screen.width - gap - size, y, size, size);
            Rect worldRect = new(Screen.width - gap - size, y - gap - size, size, size);
            Rect interactRect = new(Screen.width - gap - (size + gap) * 2f, y, size, size);
            Rect pauseRect = new(Screen.width - gap - size, gap, size, size * 0.72f);

            leftHeld = GUI.RepeatButton(leftRect, "Left", style);
            rightHeld = GUI.RepeatButton(rightRect, "Right", style);

            if (GUI.Button(jumpRect, "Jump", style))
                jumpQueued = true;
            if (GUI.Button(interactRect, "Use", style))
                interactQueued = true;
            if (GUI.Button(worldRect, "World", style))
                worldToggleQueued = true;
            if (GUI.Button(pauseRect, "Pause", style))
                pauseQueued = true;
        }

        private bool IsEnabledForCurrentPlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return simulateInEditor;
#endif
        }

        private static bool IsGameplayContext()
        {
            return InputManager.Instance == null || InputManager.Instance.CurrentContext == InputContext.Gameplay;
        }

        private static float GetUiScale()
        {
            if (Screen.dpi > 0f)
                return Mathf.Clamp(Screen.dpi / 220f, 1f, 1.7f);

            return Mathf.Clamp(Screen.height / 720f, 1f, 1.5f);
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

using System.Collections;
using BokeGameJam.Core;
using BokeGameJam.Gameplay;
using BokeGameJam.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Password keypad presentation. A future art prefab can replace the generated
    /// prototype by living at Resources/Prefabs/UI/PasswordLockPanel.
    /// </summary>
    public sealed class PasswordLockPanel : MonoBehaviour
    {
        private const string ArtPrefabResourcePath = "Prefabs/UI/PasswordLockPanel";
        private static PasswordLockPanel instance;

        [Header("View References")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text codeLabel;
        [SerializeField] private Text feedbackLabel;
        [SerializeField] private Button[] digitButtons;
        [SerializeField] private Button eraseButton;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button closeButton;

        [Header("Prototype Style")]
        [SerializeField] private Color overlayColor = new(0.02f, 0.015f, 0.02f, 0.82f);
        [SerializeField] private Color frameColor = new(0.13f, 0.09f, 0.08f, 1f);
        [SerializeField] private Color buttonColor = new(0.3f, 0.22f, 0.17f, 1f);
        [SerializeField] private Color accentColor = new(0.85f, 0.65f, 0.28f, 1f);
        [SerializeField] private Color errorColor = new(0.9f, 0.3f, 0.25f, 1f);
        [SerializeField] private Font font;

        private PasswordLockInteractable activeLock;
        private InputContext previousInputContext = InputContext.Gameplay;
        private string inputBuffer = string.Empty;
        private bool buttonsWired;
        private bool isOpen;
        private Coroutine closeCoroutine;
        private Canvas fallbackCanvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (instance != null)
                return;

            PasswordLockPanel existing = FindObjectOfType<PasswordLockPanel>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return;
            }

            GameObject artPrefab = Resources.Load<GameObject>(ArtPrefabResourcePath);
            GameObject root = artPrefab != null
                ? Instantiate(artPrefab)
                : new GameObject(nameof(PasswordLockPanel));

            if (root.GetComponent<PasswordLockPanel>() == null)
                root.AddComponent<PasswordLockPanel>();

            root.name = nameof(PasswordLockPanel);
            root.SetActive(true);
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureView();
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            EventManager.On<PasswordLockInteractable>(
                PasswordLockEvents.OpenRequested,
                Open);
        }

        private void OnDisable()
        {
            EventManager.Off<PasswordLockInteractable>(
                PasswordLockEvents.OpenRequested,
                Open);

            isOpen = false;
            RestoreInputContext();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (!isOpen)
                return;

            if (activeLock == null)
            {
                Close();
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace)
                || UnityEngine.Input.GetKeyDown(KeyCode.Delete))
            {
                RemoveLastDigit();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Return)
                || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Submit();
            }

            for (int digit = 0; digit <= 9; digit++)
            {
                KeyCode numberRowKey = (KeyCode)((int)KeyCode.Alpha0 + digit);
                KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad0 + digit);
                if (UnityEngine.Input.GetKeyDown(numberRowKey)
                    || UnityEngine.Input.GetKeyDown(keypadKey))
                {
                    AppendDigit(digit);
                }
            }
        }

        public void Open(PasswordLockInteractable passwordLock)
        {
            if (passwordLock == null || passwordLock.IsUnlocked)
                return;

            EnsureView();
            AttachToUiRoot();
            EnsureEventSystem();

            activeLock = passwordLock;
            inputBuffer = string.Empty;
            feedbackLabel.text = $"请输入 {activeLock.CodeLength} 位密码";
            feedbackLabel.color = Color.white;
            UpdateCodeLabel();

            if (InputManager.Instance != null)
            {
                previousInputContext = InputManager.Instance.CurrentContext;
                InputManager.Instance.SetContext(InputContext.UI);
            }

            isOpen = true;
            SetPanelVisible(true);
            panelRoot.SetAsLastSibling();
        }

        public void Close()
        {
            if (closeCoroutine != null)
            {
                StopCoroutine(closeCoroutine);
                closeCoroutine = null;
            }

            isOpen = false;
            activeLock = null;
            inputBuffer = string.Empty;
            SetPanelVisible(false);
            RestoreInputContext();
        }

        private void AppendDigit(int digit)
        {
            if (activeLock == null || digit < 0 || digit > 9)
                return;

            if (inputBuffer.Length >= activeLock.CodeLength)
                return;

            inputBuffer += digit.ToString();
            activeLock.NotifyDigitPressed();
            feedbackLabel.text = "请输入密码";
            feedbackLabel.color = Color.white;
            UpdateCodeLabel();
        }

        private void RemoveLastDigit()
        {
            if (activeLock == null || inputBuffer.Length == 0)
                return;

            inputBuffer = inputBuffer.Substring(0, inputBuffer.Length - 1);
            activeLock.NotifyErasePressed();
            feedbackLabel.text = "已删除上一位";
            feedbackLabel.color = Color.white;
            UpdateCodeLabel();
        }

        private void Submit()
        {
            if (activeLock == null)
                return;

            if (!activeLock.TryUnlock(inputBuffer))
            {
                feedbackLabel.text = inputBuffer.Length < activeLock.CodeLength
                    ? $"请输入完整的 {activeLock.CodeLength} 位密码"
                    : "密码错误，请重试";
                feedbackLabel.color = errorColor;
                inputBuffer = string.Empty;
                UpdateCodeLabel();
                return;
            }

            feedbackLabel.text = "密码正确，柜门已解锁";
            feedbackLabel.color = unlockedColor;
            UpdateCodeLabel();

            if (closeCoroutine != null)
                StopCoroutine(closeCoroutine);
            closeCoroutine = StartCoroutine(CloseAfterDelay(0.8f));
        }

        private IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            closeCoroutine = null;
            Close();
        }

        private void RestoreInputContext()
        {
            if (!isOpen
                && InputManager.Instance != null
                && InputManager.Instance.CurrentContext == InputContext.UI)
            {
                InputManager.Instance.SetContext(previousInputContext);
            }
        }

        private void EnsureView()
        {
            if (HasConfiguredView())
            {
                WireButtons();
                return;
            }

            BuildPrototypeView();
            WireButtons();
        }

        private bool HasConfiguredView()
        {
            return panelRoot != null
                && codeLabel != null
                && feedbackLabel != null
                && digitButtons != null
                && digitButtons.Length >= 10
                && eraseButton != null
                && submitButton != null
                && closeButton != null;
        }

        private void WireButtons()
        {
            if (buttonsWired)
                return;

            for (int digit = 0; digit <= 9; digit++)
            {
                int capturedDigit = digit;
                digitButtons[digit].onClick.AddListener(() => AppendDigit(capturedDigit));
            }

            eraseButton.onClick.AddListener(RemoveLastDigit);
            submitButton.onClick.AddListener(Submit);
            closeButton.onClick.AddListener(Close);
            buttonsWired = true;
        }

        private void BuildPrototypeView()
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panelRoot = CreateRect(
                "PrototypeRoot",
                transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            panelRoot.offsetMin = Vector2.zero;
            panelRoot.offsetMax = Vector2.zero;
            Image overlay = panelRoot.gameObject.AddComponent<Image>();
            overlay.color = overlayColor;

            RectTransform frame = CreateRect(
                "Frame",
                panelRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(560f, 660f),
                Vector2.zero);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = frameColor;

            titleLabel = CreateText(
                "Title",
                frame,
                "钟楼密码锁",
                36,
                TextAnchor.MiddleCenter,
                new Vector2(460f, 55f),
                new Vector2(0f, 275f));
            titleLabel.color = accentColor;
            titleLabel.fontStyle = FontStyle.Bold;

            codeLabel = CreateText(
                "Code",
                frame,
                string.Empty,
                38,
                TextAnchor.MiddleCenter,
                new Vector2(440f, 75f),
                new Vector2(0f, 205f));
            codeLabel.color = Color.white;

            feedbackLabel = CreateText(
                "Feedback",
                frame,
                string.Empty,
                21,
                TextAnchor.MiddleCenter,
                new Vector2(440f, 45f),
                new Vector2(0f, 145f));
            feedbackLabel.color = Color.white;

            RectTransform grid = CreateRect(
                "Keypad",
                frame,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(400f, 390f),
                new Vector2(0f, -75f));
            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(120f, 80f);
            layout.spacing = new Vector2(14f, 14f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.MiddleCenter;

            digitButtons = new Button[10];
            for (int digit = 1; digit <= 9; digit++)
                digitButtons[digit] = CreateButton($"Digit{digit}", grid, digit.ToString());

            eraseButton = CreateButton("Erase", grid, "清除");
            digitButtons[0] = CreateButton("Digit0", grid, "0");
            submitButton = CreateButton("Submit", grid, "确认");

            closeButton = CreateButton("Close", frame, "关闭");
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.sizeDelta = new Vector2(180f, 52f);
            closeRect.anchoredPosition = new Vector2(0f, 18f);
        }

        private Button CreateButton(string objectName, Transform parent, string label)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(120f, 80f),
                Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = buttonColor;
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = accentColor;
            colors.pressedColor = new Color(
                accentColor.r * 0.75f,
                accentColor.g * 0.75f,
                accentColor.b * 0.75f,
                1f);
            button.colors = colors;

            Text text = CreateText(
                "Label",
                rect,
                label,
                28,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        private Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position)
        {
            GameObject go = new(objectName, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private void AttachToUiRoot()
        {
            Transform targetParent = UIManager.Instance != null
                ? UIManager.Instance.UIRoot
                : null;

            if (targetParent == null)
                targetParent = EnsureFallbackCanvas().transform;

            if (panelRoot.parent != targetParent)
                panelRoot.SetParent(targetParent, false);

            panelRoot.anchorMin = Vector2.zero;
            panelRoot.anchorMax = Vector2.one;
            panelRoot.offsetMin = Vector2.zero;
            panelRoot.offsetMax = Vector2.zero;
            panelRoot.localScale = Vector3.one;
        }

        private Canvas EnsureFallbackCanvas()
        {
            if (fallbackCanvas != null)
                return fallbackCanvas;

            GameObject canvasObject = new(
                "PasswordLockCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            fallbackCanvas = canvasObject.GetComponent<Canvas>();
            fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fallbackCanvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return fallbackCanvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        private void UpdateCodeLabel()
        {
            if (codeLabel == null)
                return;

            int length = activeLock != null ? activeLock.CodeLength : 6;
            codeLabel.text = inputBuffer.PadRight(length, '—');
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(visible);
        }

        private Color unlockedColor => new(0.35f, 0.9f, 0.55f, 1f);
    }
}

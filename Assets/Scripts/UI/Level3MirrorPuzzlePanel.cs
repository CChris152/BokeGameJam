using System.Collections;
using BokeGameJam.Gameplay;
using BokeGameJam.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Lightweight mirror assembly subsystem for Level 3. A future art prefab at
    /// Resources/Prefabs/UI/Level3MirrorPuzzlePanel can replace the generated view.
    /// </summary>
    public sealed class Level3MirrorPuzzlePanel : MonoBehaviour
    {
        private const string ArtPrefabResourcePath = "Prefabs/UI/Level3MirrorPuzzlePanel";
        private static Level3MirrorPuzzlePanel instance;

        [Header("View References")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button[] pieceButtons;
        [SerializeField] private Image[] placedIndicators;
        [SerializeField] private Button closeButton;

        [Header("Prototype Style")]
        [SerializeField] private Color overlayColor = new(0.02f, 0.025f, 0.04f, 0.84f);
        [SerializeField] private Color frameColor = new(0.12f, 0.16f, 0.2f, 1f);
        [SerializeField] private Color emptySlotColor = new(0.2f, 0.24f, 0.28f, 1f);
        [SerializeField] private Color assembledColor = new(0.65f, 0.88f, 1f, 1f);
        [SerializeField] private Font font;

        private readonly bool[] placed = new bool[3];
        private Level3PuzzleController activeController;
        private InputContext previousInputContext = InputContext.Gameplay;
        private Canvas fallbackCanvas;
        private bool buttonsWired;
        private bool isOpen;
        private int placedCount;
        private Coroutine completionCoroutine;

        public static void Show(Level3PuzzleController controller)
        {
            if (controller == null)
                return;

            EnsureInstance();
            instance.Open(controller);
        }

        private static void EnsureInstance()
        {
            if (instance != null)
                return;

            Level3MirrorPuzzlePanel existing =
                FindObjectOfType<Level3MirrorPuzzlePanel>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                instance = existing;
                return;
            }

            GameObject artPrefab = Resources.Load<GameObject>(ArtPrefabResourcePath);
            GameObject root = artPrefab != null
                ? Instantiate(artPrefab)
                : new GameObject(nameof(Level3MirrorPuzzlePanel));
            if (root.GetComponent<Level3MirrorPuzzlePanel>() == null)
                root.AddComponent<Level3MirrorPuzzlePanel>();

            root.name = nameof(Level3MirrorPuzzlePanel);
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
            SetVisible(false);
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

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
                PlacePiece(0);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
                PlacePiece(1);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
                PlacePiece(2);
        }

        public void Open(Level3PuzzleController controller)
        {
            if (controller == null || !controller.IsMirrorAssemblyReady)
                return;

            EnsureView();
            AttachToUiRoot();
            EnsureEventSystem();

            if (activeController != controller)
            {
                activeController = controller;
                ResetAssembly();
            }

            if (InputManager.Instance != null)
            {
                previousInputContext = InputManager.Instance.CurrentContext;
                InputManager.Instance.SetContext(InputContext.UI);
            }

            isOpen = true;
            SetVisible(true);
            panelRoot.SetAsLastSibling();
            RefreshView();
        }

        public void Close()
        {
            isOpen = false;
            SetVisible(false);

            if (InputManager.Instance != null
                && InputManager.Instance.CurrentContext == InputContext.UI)
            {
                InputManager.Instance.SetContext(previousInputContext);
            }
        }

        private void PlacePiece(int index)
        {
            if (!isOpen
                || activeController == null
                || index < 0
                || index >= placed.Length
                || placed[index])
            {
                return;
            }

            placed[index] = true;
            placedCount++;
            activeController.NotifyMirrorPiecePlaced();
            RefreshView();

            if (placedCount < placed.Length || completionCoroutine != null)
                return;

            statusLabel.text = "镜面已复原，记忆正在显现……";
            completionCoroutine = StartCoroutine(CompleteAfterDelay());
        }

        private IEnumerator CompleteAfterDelay()
        {
            yield return new WaitForSecondsRealtime(0.65f);
            completionCoroutine = null;

            Level3PuzzleController controller = activeController;
            Close();
            controller?.CompleteMirrorAssembly();
        }

        private void ResetAssembly()
        {
            placedCount = 0;
            for (int i = 0; i < placed.Length; i++)
                placed[i] = false;
            RefreshView();
        }

        private void RefreshView()
        {
            if (pieceButtons == null || placedIndicators == null)
                return;

            for (int i = 0; i < placed.Length; i++)
            {
                if (i < pieceButtons.Length && pieceButtons[i] != null)
                    pieceButtons[i].interactable = !placed[i];

                if (i < placedIndicators.Length && placedIndicators[i] != null)
                {
                    placedIndicators[i].color = placed[i]
                        ? assembledColor
                        : emptySlotColor;
                }
            }

            if (statusLabel != null && placedCount < placed.Length)
                statusLabel.text = $"点击碎片放入镜框（{placedCount}/{placed.Length}）";
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
                && statusLabel != null
                && pieceButtons != null
                && pieceButtons.Length >= 3
                && placedIndicators != null
                && placedIndicators.Length >= 3
                && closeButton != null;
        }

        private void WireButtons()
        {
            if (buttonsWired)
                return;

            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                pieceButtons[i].onClick.AddListener(() => PlacePiece(captured));
            }

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
                Vector2.zero,
                Vector2.zero);
            panelRoot.offsetMin = Vector2.zero;
            panelRoot.offsetMax = Vector2.zero;
            panelRoot.gameObject.AddComponent<Image>().color = overlayColor;

            RectTransform frame = CreateRect(
                "Frame",
                panelRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(650f, 500f),
                Vector2.zero);
            frame.gameObject.AddComponent<Image>().color = frameColor;

            Text title = CreateText(
                "Title",
                frame,
                "修复镜面",
                38,
                new Vector2(500f, 60f),
                new Vector2(0f, 195f));
            title.color = assembledColor;
            title.fontStyle = FontStyle.Bold;

            statusLabel = CreateText(
                "Status",
                frame,
                string.Empty,
                22,
                new Vector2(520f, 50f),
                new Vector2(0f, 135f));

            pieceButtons = new Button[3];
            placedIndicators = new Image[3];
            string[] labels = { "左侧碎片", "中央碎片", "右侧碎片" };

            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 170f;
                RectTransform slot = CreateRect(
                    $"Slot{i + 1}",
                    frame,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(135f, 145f),
                    new Vector2(x, 30f));
                placedIndicators[i] = slot.gameObject.AddComponent<Image>();
                placedIndicators[i].color = emptySlotColor;

                RectTransform buttonRect = CreateRect(
                    $"Piece{i + 1}",
                    frame,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(145f, 58f),
                    new Vector2(x, -105f));
                buttonRect.gameObject.AddComponent<Image>().color =
                    new Color(0.32f, 0.42f, 0.52f, 1f);
                pieceButtons[i] = buttonRect.gameObject.AddComponent<Button>();

                Text label = CreateText(
                    "Label",
                    buttonRect,
                    labels[i],
                    20,
                    Vector2.zero,
                    Vector2.zero);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
            }

            closeButton = CreateButton(
                "Close",
                frame,
                "稍后继续",
                new Vector2(180f, 52f),
                new Vector2(0f, -205f));
        }

        private Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            rect.gameObject.AddComponent<Image>().color =
                new Color(0.28f, 0.34f, 0.4f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();

            Text text = CreateText(
                "Label",
                rect,
                label,
                20,
                Vector2.zero,
                Vector2.zero);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private Text CreateText(
            string objectName,
            Transform parent,
            string textValue,
            int fontSize,
            Vector2 size,
            Vector2 position)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = textValue;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position)
        {
            GameObject root = new(objectName, typeof(RectTransform));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private void AttachToUiRoot()
        {
            Transform target = UIManager.Instance != null
                ? UIManager.Instance.UIRoot
                : null;
            if (target == null)
                target = EnsureFallbackCanvas().transform;

            if (panelRoot.parent != target)
                panelRoot.SetParent(target, false);

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
                "Level3MirrorPuzzleCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            fallbackCanvas = canvasObject.GetComponent<Canvas>();
            fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fallbackCanvas.sortingOrder = 1001;

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

        private void SetVisible(bool visible)
        {
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(visible);
        }
    }
}

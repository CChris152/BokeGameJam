using TMPro;
using UnityEngine;
using BokeGameJam.Input;
using BokeGameJam.Core;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Screen Space 对话框：挂到 UI 层，用世界坐标转屏幕坐标跟随目标。
    /// 按 E / 鼠标左键 / Esc 关闭。
    /// </summary>
    public sealed class DialoguePopup : MonoBehaviour
    {
        private static DialoguePopup openInstance;
        private static int suppressInteractUntilFrame = -1;

        private const string DefaultFontResourcePath = "Art/Fonts/FZFENGRSTJW-EB SDF";

        [Header("UI")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private string fontResourcePath = DefaultFontResourcePath;
        [SerializeField] private float speakerFontSize = 22f;
        [SerializeField] private float bodyFontSize = 20f;

        [Header("Follow")]
        [SerializeField] private Transform worldAnchor;
        [SerializeField] private Vector3 worldOffset = new(0f, 1.2f, 0f);
        [SerializeField] private Vector2 screenOffset = new(0f, 40f);

        private Transform originalParent;
        private bool isOpen;
        private Canvas rootCanvas;
        private Camera uiCamera;

        public static bool IsOpen => openInstance != null && openInstance.isOpen;

        /// <summary>打开中，或本帧刚关闭（防止 E 关掉又立刻打开）。</summary>
        public static bool BlocksInteract =>
            IsOpen
            || DialogueChoicePopup.BlocksInteract
            || Time.frameCount <= suppressInteractUntilFrame;

        public static void Hide()
        {
            if (openInstance != null)
                openInstance.Close();
        }

        private void Awake()
        {
            if (panelRect == null)
                panelRect = GetComponent<RectTransform>();

            CacheLabels();
            ApplyTextStyle();

            originalParent = transform.parent;
            // Prefab 默认 inactive；不要在 Awake 里 SetActive(false)。
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (panelRect == null)
                panelRect = GetComponent<RectTransform>();
            CacheLabels();
            ApplyTextStyle();
        }
#endif

        private void CacheLabels()
        {
            if (speakerLabel == null)
            {
                Transform t = transform.Find("Speaker");
                if (t != null)
                    speakerLabel = t.GetComponent<TMP_Text>();
            }

            if (bodyLabel == null)
            {
                Transform t = transform.Find("Body");
                if (t != null)
                    bodyLabel = t.GetComponent<TMP_Text>();
            }
        }

        private void ApplyTextStyle()
        {
            TMP_FontAsset font = ResolveFont();

            if (speakerLabel != null)
            {
                if (font != null)
                    speakerLabel.font = font;
                speakerLabel.fontSize = Mathf.Max(1f, speakerFontSize);
            }

            if (bodyLabel != null)
            {
                if (font != null)
                    bodyLabel.font = font;
                bodyLabel.fontSize = Mathf.Max(1f, bodyFontSize);
            }
        }

        private TMP_FontAsset ResolveFont()
        {
            if (fontAsset != null)
                return fontAsset;

            string path = string.IsNullOrWhiteSpace(fontResourcePath)
                ? DefaultFontResourcePath
                : fontResourcePath.Trim();

            fontAsset = Resources.Load<TMP_FontAsset>(path);
            if (fontAsset == null && path != DefaultFontResourcePath)
                fontAsset = Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);

            return fontAsset;
        }

        private void OnEnable()
        {
            EventManager.On(InputEvents.PlayerInteractPressed, OnInteractOrClose);
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.PlayerInteractPressed, OnInteractOrClose);
            if (openInstance == this)
            {
                isOpen = false;
                openInstance = null;
            }
        }

        private void LateUpdate()
        {
            if (!isOpen)
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)
                || UnityEngine.Input.GetMouseButtonDown(0))
            {
                Close();
                return;
            }

            UpdateScreenPosition();
        }

        /// <summary>显示对话，并跟随世界锚点投影到 UI。</summary>
        public void Show(string speaker, string body, Transform anchor = null)
        {
            if (openInstance != null && openInstance != this)
                openInstance.Close();

            if (anchor != null)
                worldAnchor = anchor;
            else if (worldAnchor == null && originalParent != null)
                worldAnchor = originalParent;

            CacheLabels();
            ApplyTextStyle();

            if (speakerLabel != null)
                speakerLabel.text = string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker.Trim();

            if (bodyLabel != null)
                bodyLabel.text = body ?? string.Empty;

            AttachToUiRoot();
            isOpen = true;
            openInstance = this;
            gameObject.SetActive(true);
            BringToFront();
            UpdateScreenPosition();
        }

        public void Close()
        {
            isOpen = false;
            if (openInstance == this)
                openInstance = null;

            suppressInteractUntilFrame = Time.frameCount;
            RestoreOriginalParent();
            gameObject.SetActive(false);
        }

        private void OnInteractOrClose()
        {
            if (isOpen)
                Close();
        }

        private void AttachToUiRoot()
        {
            Transform uiRoot = UIManager.Instance != null ? UIManager.Instance.UIRoot : null;
            if (uiRoot == null)
            {
                Debug.LogWarning("[DialoguePopup] UIManager.UIRoot 未找到，对话框留在原父节点。", this);
                rootCanvas = GetComponentInParent<Canvas>();
                uiCamera = null;
                return;
            }

            if (transform.parent != uiRoot)
                transform.SetParent(uiRoot, false);

            rootCanvas = uiRoot.GetComponentInParent<Canvas>();
            uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0f);
                panelRect.localScale = Vector3.one;
                panelRect.localRotation = Quaternion.identity;
            }

            BringToFront();
        }

        /// <summary>提到 UI 根最前，避免被 Inventory / Banner 等盖住。</summary>
        private void BringToFront()
        {
            transform.SetAsLastSibling();
        }

        private void RestoreOriginalParent()
        {
            if (originalParent == null || transform.parent == originalParent)
                return;

            // Unity forbids SetParent while a hierarchy is activating/deactivating
            // (e.g. world toggle disables the ghost and Close runs from OnDisable).
            // Leave the panel under UIRoot; Show() re-attaches next time.
            if (!originalParent.gameObject.activeInHierarchy)
                return;

            transform.SetParent(originalParent, false);
            if (panelRect != null)
            {
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.localScale = Vector3.one;
            }
        }

        private void OnDestroy()
        {
            if (openInstance == this)
            {
                isOpen = false;
                openInstance = null;
            }
        }

        private void UpdateScreenPosition()
        {
            if (panelRect == null || worldAnchor == null)
                return;

            // 打开期间保持在最上层（其他 UI 可能后加载）。
            if (isOpen)
                BringToFront();

            Camera worldCam = Camera.main;
            if (worldCam == null)
                return;

            Vector3 worldPos = worldAnchor.position + worldOffset;
            Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);

            // 在相机后方则移出屏幕
            if (screenPos.z < 0f)
            {
                panelRect.anchoredPosition = new Vector2(-99999f, -99999f);
                return;
            }

            screenPos.x += screenOffset.x;
            screenPos.y += screenOffset.y;

            RectTransform parentRect = panelRect.parent as RectTransform;
            if (parentRect == null)
            {
                panelRect.position = screenPos;
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPos,
                    uiCamera,
                    out Vector2 localPoint))
            {
                panelRect.anchoredPosition = localPoint;
            }
        }
    }
}

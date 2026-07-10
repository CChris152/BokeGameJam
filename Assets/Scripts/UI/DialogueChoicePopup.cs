using System;
using UnityEngine;
using UnityEngine.UI;
using BokeGameJam.Input;
using BokeGameJam.Core;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 鬼魂互动选项面板：持有物品 A 时优先弹出，按钮点击后回调。
    /// 按 E / Esc 关闭且不执行选项。
    /// </summary>
    public sealed class DialogueChoicePopup : MonoBehaviour
    {
        private static DialogueChoicePopup openInstance;
        private static int suppressInteractUntilFrame = -1;

        [Header("UI")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryButtonLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private Text secondaryButtonLabel;

        [Header("Follow")]
        [SerializeField] private Transform worldAnchor;
        [SerializeField] private Vector3 worldOffset = new(0f, 1.2f, 0f);
        [SerializeField] private Vector2 screenOffset = new(0f, 40f);

        private Transform originalParent;
        private bool isOpen;
        private Canvas rootCanvas;
        private Camera uiCamera;
        private Action onPrimary;
        private Action onSecondary;

        public static bool IsOpen => openInstance != null && openInstance.isOpen;

        /// <summary>打开中，或本帧刚关闭（防止 E 关掉又立刻打开）。</summary>
        public static bool BlocksInteract =>
            IsOpen || Time.frameCount <= suppressInteractUntilFrame;

        public static void Hide()
        {
            if (openInstance != null)
                openInstance.Close();
        }

        private void Awake()
        {
            if (panelRect == null)
                panelRect = GetComponent<RectTransform>();

            CacheRefs();
            originalParent = transform.parent;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (panelRect == null)
                panelRect = GetComponent<RectTransform>();
            CacheRefs();
        }
#endif

        private void CacheRefs()
        {
            if (titleLabel == null)
            {
                Transform t = transform.Find("Title");
                if (t != null)
                    titleLabel = t.GetComponent<Text>();
            }

            if (primaryButton == null)
            {
                Transform t = transform.Find("ButtonPrimary");
                if (t != null)
                    primaryButton = t.GetComponent<Button>();
            }

            if (primaryButtonLabel == null && primaryButton != null)
            {
                Transform label = primaryButton.transform.Find("Label");
                if (label != null)
                    primaryButtonLabel = label.GetComponent<Text>();
                else
                    primaryButtonLabel = primaryButton.GetComponentInChildren<Text>(true);
            }

            if (secondaryButton == null)
            {
                Transform t = transform.Find("ButtonSecondary");
                if (t != null)
                    secondaryButton = t.GetComponent<Button>();
            }

            if (secondaryButtonLabel == null && secondaryButton != null)
            {
                Transform label = secondaryButton.transform.Find("Label");
                if (label != null)
                    secondaryButtonLabel = label.GetComponent<Text>();
                else
                    secondaryButtonLabel = secondaryButton.GetComponentInChildren<Text>(true);
            }
        }

        private void OnEnable()
        {
            EventManager.On(InputEvents.PlayerInteractPressed, OnInteractOrClose);
            WireButtons();
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.PlayerInteractPressed, OnInteractOrClose);
            UnwireButtons();
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

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            UpdateScreenPosition();
        }

        /// <summary>
        /// 显示双选项面板。primary / secondary 为按钮文案；回调在关闭面板后执行。
        /// </summary>
        public void Show(
            string title,
            string primaryLabel,
            string secondaryLabel,
            Action primaryAction,
            Action secondaryAction,
            Transform anchor = null)
        {
            if (openInstance != null && openInstance != this)
                openInstance.Close();

            if (DialoguePopup.IsOpen)
                DialoguePopup.Hide();

            if (anchor != null)
                worldAnchor = anchor;
            else if (worldAnchor == null && originalParent != null)
                worldAnchor = originalParent;

            CacheRefs();
            onPrimary = primaryAction;
            onSecondary = secondaryAction;

            if (titleLabel != null)
                titleLabel.text = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();

            if (primaryButtonLabel != null)
                primaryButtonLabel.text = string.IsNullOrWhiteSpace(primaryLabel) ? "确认" : primaryLabel.Trim();

            if (secondaryButtonLabel != null)
                secondaryButtonLabel.text = string.IsNullOrWhiteSpace(secondaryLabel) ? "取消" : secondaryLabel.Trim();

            AttachToUiRoot();
            isOpen = true;
            openInstance = this;
            gameObject.SetActive(true);
            UpdateScreenPosition();
        }

        public void Close()
        {
            isOpen = false;
            if (openInstance == this)
                openInstance = null;

            onPrimary = null;
            onSecondary = null;
            suppressInteractUntilFrame = Time.frameCount;
            RestoreOriginalParent();
            gameObject.SetActive(false);
        }

        private void WireButtons()
        {
            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(OnPrimaryClicked);
                primaryButton.onClick.AddListener(OnPrimaryClicked);
            }

            if (secondaryButton != null)
            {
                secondaryButton.onClick.RemoveListener(OnSecondaryClicked);
                secondaryButton.onClick.AddListener(OnSecondaryClicked);
            }
        }

        private void UnwireButtons()
        {
            if (primaryButton != null)
                primaryButton.onClick.RemoveListener(OnPrimaryClicked);
            if (secondaryButton != null)
                secondaryButton.onClick.RemoveListener(OnSecondaryClicked);
        }

        private void OnPrimaryClicked()
        {
            Action action = onPrimary;
            Close();
            action?.Invoke();
        }

        private void OnSecondaryClicked()
        {
            Action action = onSecondary;
            Close();
            action?.Invoke();
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
                Debug.LogWarning("[DialogueChoicePopup] UIManager.UIRoot 未找到，选项面板留在原父节点。", this);
                rootCanvas = GetComponentInParent<Canvas>();
                uiCamera = null;
                return;
            }

            if (transform.parent != uiRoot)
                transform.SetParent(uiRoot, false);

            transform.SetAsLastSibling();
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
        }

        private void RestoreOriginalParent()
        {
            if (originalParent == null || transform.parent == originalParent)
                return;

            // Unity forbids SetParent while a hierarchy is activating/deactivating
            // (e.g. world toggle disables the owner and Close runs from OnDisable).
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

        private void UpdateScreenPosition()
        {
            if (panelRect == null || worldAnchor == null)
                return;

            Camera worldCam = Camera.main;
            if (worldCam == null)
                return;

            Vector3 worldPos = worldAnchor.position + worldOffset;
            Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);

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

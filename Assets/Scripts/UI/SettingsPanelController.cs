using System.Collections;
using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 设置弹窗控制器：清空数据、音量调节、关闭弹窗。
    /// 打开时可在 Inspector 切换两种演示动画：淡入 / 从上滑入。
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        public const string ResourceId = "SettingsPanel";

        /// <summary>打开动画方案（给策划演示用，可在 Inspector 切换）。</summary>
        public enum OpenAnimationStyle
        {
            /// <summary>中间内容从透明变为不透明。</summary>
            FadeIn = 0,
            /// <summary>中间内容从界面上方弹到中间（带缓动）。</summary>
            SlideFromTop = 1
        }

        [Header("按钮")]
        [Tooltip("清空存档数据按钮（逻辑暂未实现）")]
        [SerializeField] private Button clearDataButton;

        [Tooltip("右上角关闭按钮")]
        [SerializeField] private Button closeButton;

        [Header("音量")]
        [Tooltip("主音量滑条（BGM 与 SFX 共用）")]
        [SerializeField] private Slider volumeSlider;

        [Header("打开动画（演示切换）")]
        [Tooltip("中间内容面板（通常是 Panel）")]
        [SerializeField] private RectTransform contentPanel;

        [Tooltip("打开动画方案：FadeIn=淡入，SlideFromTop=从上滑入")]
        [SerializeField] private OpenAnimationStyle openAnimation = OpenAnimationStyle.FadeIn;

        [Tooltip("打开动画时长（秒）")]
        [SerializeField] private float openDuration = 0.35f;

        [Tooltip("从上滑入时，起始位置相对屏幕高度的偏移倍数（越大越靠上）")]
        [SerializeField] private float slideFromTopOffsetFactor = 1.2f;

        private CanvasGroup contentCanvasGroup;
        private Vector2 contentRestPosition;
        private Coroutine openRoutine;

        private void Awake()
        {
            // 绑定按钮与滑条事件，避免重复订阅先移除再添加
            if (clearDataButton != null)
            {
                clearDataButton.onClick.RemoveListener(OnClearDataClicked);
                clearDataButton.onClick.AddListener(OnClearDataClicked);
            }
            else
            {
                Debug.LogWarning("[SettingsPanelController] Clear data button is missing.", this);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
                closeButton.onClick.AddListener(OnCloseClicked);
            }
            else
            {
                Debug.LogWarning("[SettingsPanelController] Close button is missing.", this);
            }

            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }
            else
            {
                Debug.LogWarning("[SettingsPanelController] Volume slider is missing.", this);
            }

            ResolveContentPanel();
            if (contentPanel != null)
                contentRestPosition = contentPanel.anchoredPosition;
        }

        private void OnEnable()
        {
            // 每次显示时从存档同步滑条与运行时音量
            SyncVolumeFromSavedData();
            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            if (openRoutine != null)
            {
                StopCoroutine(openRoutine);
                openRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (clearDataButton != null)
                clearDataButton.onClick.RemoveListener(OnClearDataClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseClicked);

            if (volumeSlider != null)
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        }

        /// <summary>
        /// 读取已保存的主音量，刷新滑条并应用到音频管理器。
        /// </summary>
        private void SyncVolumeFromSavedData()
        {
            if (volumeSlider == null)
                return;

            float volume = ResolveSavedMasterVolume();
            // 不触发 onValueChanged，避免打开面板时重复写盘
            volumeSlider.SetValueWithoutNotify(volume);
            ApplyMasterVolume(volume);
        }

        /// <summary>
        /// 解析当前应显示的主音量：优先 DataManager，其次运行时音频值，最后回退默认值。
        /// </summary>
        private float ResolveSavedMasterVolume()
        {
            if (DataManager.Instance != null)
                return Mathf.Clamp01(DataManager.Instance.GetFloat(DataManager.Keys.MasterVolume));

            if (GameAudioManager.Instance != null)
                return GameAudioManager.Instance.BgmVolume;

            return 0.6f;
        }

        /// <summary>滑条拖动回调：同时更新运行时音量并持久化。</summary>
        private void OnVolumeChanged(float value)
        {
            float volume = Mathf.Clamp01(value);
            ApplyMasterVolume(volume);
            PersistMasterVolume(volume);
        }

        /// <summary>将主音量应用到 BGM 与 SFX（当前共用同一数值）。</summary>
        private static void ApplyMasterVolume(float volume)
        {
            if (GameAudioManager.Instance == null)
            {
                Debug.LogWarning("[SettingsPanelController] GameAudioManager instance is missing.");
                return;
            }

            // 目前 BGM 与 SFX 共用同一主音量
            GameAudioManager.Instance.SetBGMVolume(volume);
            GameAudioManager.Instance.SetSFXVolume(volume);
        }

        /// <summary>通过 DataManager 将主音量写入 PlayerPrefs。</summary>
        private static void PersistMasterVolume(float volume)
        {
            if (DataManager.Instance == null)
            {
                Debug.LogWarning("[SettingsPanelController] DataManager instance is missing. Volume was not saved.");
                return;
            }

            DataManager.Instance.SetFloat(DataManager.Keys.MasterVolume, volume);
        }

        /// <summary>清空数据按钮回调（占位，后续接入存档清理）。</summary>
        private void OnClearDataClicked()
        {
            // TODO: 清除存档数据
        }

        /// <summary>关闭按钮：通过 UIManager 卸载本弹窗。</summary>
        private void OnCloseClicked()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[SettingsPanelController] UIManager instance is missing.", this);
                Destroy(gameObject);
                return;
            }

            UIManager.Instance.Close(ResourceId);
        }

        /// <summary>按当前方案播放打开动画。</summary>
        private void PlayOpenAnimation()
        {
            ResolveContentPanel();
            if (contentPanel == null)
                return;

            if (openRoutine != null)
                StopCoroutine(openRoutine);

            EnsureContentCanvasGroup();
            contentPanel.anchoredPosition = contentRestPosition;

            switch (openAnimation)
            {
                case OpenAnimationStyle.SlideFromTop:
                    openRoutine = StartCoroutine(SlideFromTopRoutine());
                    break;
                default:
                    openRoutine = StartCoroutine(FadeInRoutine());
                    break;
            }
        }

        private IEnumerator FadeInRoutine()
        {
            float duration = Mathf.Max(0.01f, openDuration);
            contentCanvasGroup.alpha = 0f;
            contentCanvasGroup.interactable = false;
            contentCanvasGroup.blocksRaycasts = false;
            contentPanel.anchoredPosition = contentRestPosition;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smoothstep：中间更快、两端更柔
                t = t * t * (3f - 2f * t);
                contentCanvasGroup.alpha = t;
                yield return null;
            }

            contentCanvasGroup.alpha = 1f;
            contentCanvasGroup.interactable = true;
            contentCanvasGroup.blocksRaycasts = true;
            openRoutine = null;
        }

        private IEnumerator SlideFromTopRoutine()
        {
            float duration = Mathf.Max(0.01f, openDuration);
            float startY = ResolveSlideStartY();
            Vector2 startPos = new Vector2(contentRestPosition.x, startY);

            contentCanvasGroup.alpha = 1f;
            contentCanvasGroup.interactable = false;
            contentCanvasGroup.blocksRaycasts = false;
            contentPanel.anchoredPosition = startPos;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // EaseOutCubic：开始快、落地前减速，更像“弹到中间”
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                contentPanel.anchoredPosition = Vector2.LerpUnclamped(startPos, contentRestPosition, eased);
                yield return null;
            }

            contentPanel.anchoredPosition = contentRestPosition;
            contentCanvasGroup.interactable = true;
            contentCanvasGroup.blocksRaycasts = true;
            openRoutine = null;
        }

        private float ResolveSlideStartY()
        {
            float panelHeight = contentPanel.rect.height;
            RectTransform parent = contentPanel.parent as RectTransform;
            float parentHeight = parent != null ? parent.rect.height : Screen.height;
            // 放到父节点上方之外，再按系数拉开一点距离
            return contentRestPosition.y + parentHeight * Mathf.Max(0.5f, slideFromTopOffsetFactor) + panelHeight;
        }

        private void ResolveContentPanel()
        {
            if (contentPanel != null)
                return;

            Transform panel = transform.Find("Panel");
            if (panel != null)
                contentPanel = panel as RectTransform;
        }

        private void EnsureContentCanvasGroup()
        {
            if (contentPanel == null)
                return;

            contentCanvasGroup = contentPanel.GetComponent<CanvasGroup>();
            if (contentCanvasGroup == null)
                contentCanvasGroup = contentPanel.gameObject.AddComponent<CanvasGroup>();
        }
    }
}

using System.Collections;
using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 设置弹窗控制器：清空数据、音乐/音效音量调节、关闭弹窗。
    /// 打开时中间内容面板播放淡入动画。
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        public const string ResourceId = "SettingsPanel";

        private const float DefaultBgmVolume = 0.6f;
        private const float DefaultSfxVolume = 1f;

        [Header("按钮")]
        [Tooltip("清空存档数据按钮（逻辑暂未实现）")]
        [SerializeField] private Button clearDataButton;

        [Tooltip("右上角关闭按钮")]
        [SerializeField] private Button closeButton;

        [Header("音量")]
        [Tooltip("音乐（BGM）音量滑条")]
        [SerializeField] private Slider bgmVolumeSlider;

        [Tooltip("音效（SFX）音量滑条")]
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("打开动画")]
        [Tooltip("中间内容面板（通常是 Panel）")]
        [SerializeField] private RectTransform contentPanel;

        [Tooltip("打开动画时长（秒）")]
        [SerializeField] private float openDuration = 0.35f;

        private CanvasGroup contentCanvasGroup;
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

            BindVolumeSlider(bgmVolumeSlider, OnBgmVolumeChanged, "BGM volume slider");
            BindVolumeSlider(sfxVolumeSlider, OnSfxVolumeChanged, "SFX volume slider");

            ResolveContentPanel();
        }

        private void OnEnable()
        {
            // 每次显示时从存档同步滑条与运行时音量
            SyncVolumesFromSavedData();
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

            UnbindVolumeSlider(bgmVolumeSlider, OnBgmVolumeChanged);
            UnbindVolumeSlider(sfxVolumeSlider, OnSfxVolumeChanged);
        }

        /// <summary>绑定单个音量滑条（0-1）。</summary>
        private void BindVolumeSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback, string missingLabel)
        {
            if (slider == null)
            {
                Debug.LogWarning($"[SettingsPanelController] {missingLabel} is missing.", this);
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.RemoveListener(callback);
            slider.onValueChanged.AddListener(callback);
        }

        private static void UnbindVolumeSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(callback);
        }

        /// <summary>
        /// 读取已保存的音乐/音效音量，刷新滑条并应用到音频管理器。
        /// </summary>
        private void SyncVolumesFromSavedData()
        {
            float bgmVolume = ResolveSavedVolume(DataManager.Keys.BgmVolume, DefaultBgmVolume, useRuntimeBgm: true);
            float sfxVolume = ResolveSavedVolume(DataManager.Keys.SfxVolume, DefaultSfxVolume, useRuntimeBgm: false);

            if (bgmVolumeSlider != null)
                bgmVolumeSlider.SetValueWithoutNotify(bgmVolume);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);

            ApplyBgmVolume(bgmVolume);
            ApplySfxVolume(sfxVolume);
        }

        /// <summary>
        /// 解析已保存音量：优先独立键，其次旧版 MasterVolume，再回退运行时/默认值。
        /// </summary>
        private static float ResolveSavedVolume(string volumeKey, float defaultValue, bool useRuntimeBgm)
        {
            if (DataManager.Instance != null)
            {
                if (DataManager.Instance.HasKey(volumeKey))
                    return Mathf.Clamp01(DataManager.Instance.GetFloat(volumeKey));

                if (DataManager.Instance.HasKey(DataManager.Keys.MasterVolume))
                    return Mathf.Clamp01(DataManager.Instance.GetFloat(DataManager.Keys.MasterVolume));

                return Mathf.Clamp01(DataManager.Instance.GetFloat(volumeKey, defaultValue));
            }

            if (GameAudioManager.Instance != null)
                return useRuntimeBgm ? GameAudioManager.Instance.BgmVolume : GameAudioManager.Instance.SfxVolume;

            return defaultValue;
        }

        private void OnBgmVolumeChanged(float value)
        {
            float volume = Mathf.Clamp01(value);
            ApplyBgmVolume(volume);
            PersistVolume(DataManager.Keys.BgmVolume, volume);
        }

        private void OnSfxVolumeChanged(float value)
        {
            float volume = Mathf.Clamp01(value);
            ApplySfxVolume(volume);
            PersistVolume(DataManager.Keys.SfxVolume, volume);
        }

        private static void ApplyBgmVolume(float volume)
        {
            if (GameAudioManager.Instance == null)
            {
                Debug.LogWarning("[SettingsPanelController] GameAudioManager instance is missing.");
                return;
            }

            GameAudioManager.Instance.SetBGMVolume(volume);
        }

        private static void ApplySfxVolume(float volume)
        {
            if (GameAudioManager.Instance == null)
            {
                Debug.LogWarning("[SettingsPanelController] GameAudioManager instance is missing.");
                return;
            }

            GameAudioManager.Instance.SetSFXVolume(volume);
        }

        /// <summary>通过 DataManager 将音量写入 PlayerPrefs。</summary>
        private static void PersistVolume(string volumeKey, float volume)
        {
            if (DataManager.Instance == null)
            {
                Debug.LogWarning($"[SettingsPanelController] DataManager instance is missing. Volume '{volumeKey}' was not saved.");
                return;
            }

            DataManager.Instance.SetFloat(volumeKey, volume);
        }

        /// <summary>清空数据按钮回调（占位，后续接入存档清理）。</summary>
        private void OnClearDataClicked()
        {
            if (GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.UiConfirm);

            // TODO: 清除存档数据
        }

        /// <summary>关闭按钮：通过 UIManager 卸载本弹窗。</summary>
        private void OnCloseClicked()
        {
            if (GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.UiBack);

            if (UIManager.Instance == null)
            {
                Debug.LogError("[SettingsPanelController] UIManager instance is missing.", this);
                Destroy(gameObject);
                return;
            }

            UIManager.Instance.Close(ResourceId);
        }

        /// <summary>播放打开淡入动画。</summary>
        private void PlayOpenAnimation()
        {
            ResolveContentPanel();
            if (contentPanel == null)
                return;

            if (openRoutine != null)
                StopCoroutine(openRoutine);

            EnsureContentCanvasGroup();
            openRoutine = StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            float duration = Mathf.Max(0.01f, openDuration);
            contentCanvasGroup.alpha = 0f;
            contentCanvasGroup.interactable = false;
            contentCanvasGroup.blocksRaycasts = false;

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

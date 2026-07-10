using System.Collections;
using BokeGameJam.Core;
using BokeGameJam.Input;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 暂停弹窗控制器：返回主菜单、音乐/音效音量调节、关闭弹窗。
    /// 打开时不暂停时间，仅切换到 UI 输入上下文以屏蔽游戏按键；ESC 仍由 PauseMenuTrigger 处理。
    /// 通常由 <see cref="PauseMenuTrigger"/> 在按下 Escape 时打开。
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public const string ResourceId = "PauseMenu";
        private const string StartSceneId = "StartScene";

        private const float DefaultBgmVolume = 0.6f;
        private const float DefaultSfxVolume = 1f;

        [Header("按钮")]
        [Tooltip("退回主菜单按钮")]
        [SerializeField] private Button returnToMainMenuButton;

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
        private InputContext previousInputContext = InputContext.Gameplay;
        private bool pauseStateApplied;

        private void Awake()
        {
            // 绑定按钮与滑条事件，避免重复订阅先移除再添加
            if (returnToMainMenuButton != null)
            {
                returnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);
                returnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);
            }
            else
            {
                Debug.LogWarning("[PauseMenuController] Return to main menu button is missing.", this);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
                closeButton.onClick.AddListener(OnCloseClicked);
            }
            else
            {
                Debug.LogWarning("[PauseMenuController] Close button is missing.", this);
            }

            BindVolumeSlider(bgmVolumeSlider, OnBgmVolumeChanged, "BGM volume slider");
            BindVolumeSlider(sfxVolumeSlider, OnSfxVolumeChanged, "SFX volume slider");

            ResolveContentPanel();
        }

        private void OnEnable()
        {
            ApplyPauseState();
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

            RestorePauseState();
        }

        private void OnDestroy()
        {
            RestorePauseState();

            if (returnToMainMenuButton != null)
                returnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseClicked);

            UnbindVolumeSlider(bgmVolumeSlider, OnBgmVolumeChanged);
            UnbindVolumeSlider(sfxVolumeSlider, OnSfxVolumeChanged);
        }

        private void BindVolumeSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback, string missingLabel)
        {
            if (slider == null)
            {
                Debug.LogWarning($"[PauseMenuController] {missingLabel} is missing.", this);
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
        /// 打开暂停页：不暂停时间，只切到 UI 输入上下文，屏蔽 WASD / E / Shift 等游戏输入。
        /// ESC 由 <see cref="PauseMenuTrigger"/> 单独处理，不受此上下文影响。
        /// </summary>
        private void ApplyPauseState()
        {
            if (pauseStateApplied)
                return;

            // 若此前误把 timeScale 置 0，打开暂停页时先恢复，避免卡死。
            if (Time.timeScale <= 0f)
                Time.timeScale = 1f;

            if (InputManager.Instance != null)
            {
                previousInputContext = InputManager.Instance.CurrentContext;
                InputManager.Instance.SetContext(InputContext.UI);
            }

            pauseStateApplied = true;
        }

        private void RestorePauseState()
        {
            if (!pauseStateApplied)
                return;

            if (InputManager.Instance != null && InputManager.Instance.CurrentContext == InputContext.UI)
                InputManager.Instance.SetContext(previousInputContext);

            pauseStateApplied = false;
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
                Debug.LogWarning("[PauseMenuController] GameAudioManager instance is missing.");
                return;
            }

            GameAudioManager.Instance.SetBGMVolume(volume);
        }

        private static void ApplySfxVolume(float volume)
        {
            if (GameAudioManager.Instance == null)
            {
                Debug.LogWarning("[PauseMenuController] GameAudioManager instance is missing.");
                return;
            }

            GameAudioManager.Instance.SetSFXVolume(volume);
        }

        private static void PersistVolume(string volumeKey, float volume)
        {
            if (DataManager.Instance == null)
            {
                Debug.LogWarning($"[PauseMenuController] DataManager instance is missing. Volume '{volumeKey}' was not saved.");
                return;
            }

            DataManager.Instance.SetFloat(volumeKey, volume);
        }

        /// <summary>
        /// 退回主菜单：先关闭本弹窗，再播加载过渡（透明→全黑→透明），
        /// 在全黑时切换到 StartScene。
        /// </summary>
        private void OnReturnToMainMenuClicked()
        {
            CloseSelf();

            if (GameSceneManager.Instance == null)
            {
                Debug.LogError("[PauseMenuController] GameSceneManager instance is missing.", this);
                return;
            }

            if (BlackScreenLoader.Instance == null)
            {
                Debug.LogWarning("[PauseMenuController] BlackScreenLoader missing, return to main menu immediately.", this);
                GameSceneManager.Instance.LoadSceneById(StartSceneId);
                return;
            }

            BlackScreenLoader.Instance.PlayLoadingAnimation(() =>
            {
                if (GameSceneManager.Instance == null)
                {
                    Debug.LogError("[PauseMenuController] GameSceneManager instance is missing during return.", this);
                    return;
                }

                GameSceneManager.Instance.LoadSceneById(StartSceneId);
            });
        }

        /// <summary>关闭按钮回调。</summary>
        private void OnCloseClicked()
        {
            CloseSelf();
        }

        /// <summary>通过 UIManager 卸载本弹窗；若管理器缺失则直接销毁自身。</summary>
        private void CloseSelf()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[PauseMenuController] UIManager instance is missing.", this);
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

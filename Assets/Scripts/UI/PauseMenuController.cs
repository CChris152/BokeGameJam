using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 暂停弹窗控制器：返回主菜单、音量调节、关闭弹窗。
    /// 通常由 <see cref="PauseMenuTrigger"/> 在按下 Escape 时打开。
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public const string ResourceId = "PauseMenu";
        private const string StartSceneId = "StartScene";

        [Header("按钮")]
        [Tooltip("退回主菜单按钮")]
        [SerializeField] private Button returnToMainMenuButton;

        [Tooltip("右上角关闭按钮")]
        [SerializeField] private Button closeButton;

        [Header("音量")]
        [Tooltip("主音量滑条（BGM 与 SFX 共用，逻辑与设置面板一致）")]
        [SerializeField] private Slider volumeSlider;

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

            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }
            else
            {
                Debug.LogWarning("[PauseMenuController] Volume slider is missing.", this);
            }
        }

        private void OnEnable()
        {
            // 每次显示时从存档同步滑条与运行时音量
            SyncVolumeFromSavedData();
        }

        private void OnDestroy()
        {
            if (returnToMainMenuButton != null)
                returnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);

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
                Debug.LogWarning("[PauseMenuController] GameAudioManager instance is missing.");
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
                Debug.LogWarning("[PauseMenuController] DataManager instance is missing. Volume was not saved.");
                return;
            }

            DataManager.Instance.SetFloat(DataManager.Keys.MasterVolume, volume);
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
    }
}

using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 设置弹窗控制器：清空数据、音量调节、关闭弹窗。
    /// 通过 <see cref="UIManager"/> 按 id <see cref="UiId"/> 加载/卸载。
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        /// <summary>资源库中登记的 UI id，需与 ResourceDefinitionDatabase 保持一致。</summary>
        public const string UiId = "SettingsPanel";

        [Header("按钮")]
        [Tooltip("清空存档数据按钮（逻辑暂未实现）")]
        [SerializeField] private Button clearDataButton;

        [Tooltip("右上角关闭按钮")]
        [SerializeField] private Button closeButton;

        [Header("音量")]
        [Tooltip("主音量滑条（BGM 与 SFX 共用）")]
        [SerializeField] private Slider volumeSlider;

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
        }

        private void OnEnable()
        {
            // 每次显示时从存档同步滑条与运行时音量
            SyncVolumeFromSavedData();
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

            UIManager.Instance.CloseUIById(UiId);
        }
    }
}

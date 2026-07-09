using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Controls the settings popup: clear data, volume, and close.
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        public const string UiId = "SettingsPanel";

        [SerializeField] private Button clearDataButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Slider volumeSlider;

        private void Awake()
        {
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

        private void SyncVolumeFromSavedData()
        {
            if (volumeSlider == null)
                return;

            float volume = ResolveSavedMasterVolume();
            volumeSlider.SetValueWithoutNotify(volume);
            ApplyMasterVolume(volume);
        }

        private float ResolveSavedMasterVolume()
        {
            if (DataManager.Instance != null)
                return Mathf.Clamp01(DataManager.Instance.GetFloat(DataManager.Keys.MasterVolume));

            if (GameAudioManager.Instance != null)
                return GameAudioManager.Instance.BgmVolume;

            return 0.6f;
        }

        private void OnVolumeChanged(float value)
        {
            float volume = Mathf.Clamp01(value);
            ApplyMasterVolume(volume);
            PersistMasterVolume(volume);
        }

        private static void ApplyMasterVolume(float volume)
        {
            if (GameAudioManager.Instance == null)
            {
                Debug.LogWarning("[SettingsPanelController] GameAudioManager instance is missing.");
                return;
            }

            // BGM and SFX share one master volume for now.
            GameAudioManager.Instance.SetBGMVolume(volume);
            GameAudioManager.Instance.SetSFXVolume(volume);
        }

        private static void PersistMasterVolume(float volume)
        {
            if (DataManager.Instance == null)
            {
                Debug.LogWarning("[SettingsPanelController] DataManager instance is missing. Volume was not saved.");
                return;
            }

            DataManager.Instance.SetFloat(DataManager.Keys.MasterVolume, volume);
        }

        private void OnClearDataClicked()
        {
            // TODO: clear save data
        }

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

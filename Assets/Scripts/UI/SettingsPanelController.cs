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
            SyncVolumeFromAudioManager();
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

        private void SyncVolumeFromAudioManager()
        {
            if (volumeSlider == null || GameAudioManager.Instance == null)
                return;

            volumeSlider.SetValueWithoutNotify(GameAudioManager.Instance.BgmVolume);
        }

        private void OnVolumeChanged(float value)
        {
            if (GameAudioManager.Instance == null)
            {
                Debug.LogWarning("[SettingsPanelController] GameAudioManager instance is missing.", this);
                return;
            }

            // Runtime only; do not persist to PlayerPrefs yet.
            GameAudioManager.Instance.SetBGMVolume(value);
            GameAudioManager.Instance.SetSFXVolume(value);
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

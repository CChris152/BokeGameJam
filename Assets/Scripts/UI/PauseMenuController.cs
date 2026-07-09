using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 控制暂停弹窗：返回主菜单、音量调节与关闭。
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public const string ResourceId = "PauseMenu";
        private const string StartSceneId = "StartScene";

        [SerializeField] private Button returnToMainMenuButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Slider volumeSlider;

        private void Awake()
        {
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
                Debug.LogWarning("[PauseMenuController] GameAudioManager instance is missing.");
                return;
            }

            GameAudioManager.Instance.SetBGMVolume(volume);
            GameAudioManager.Instance.SetSFXVolume(volume);
        }

        private static void PersistMasterVolume(float volume)
        {
            if (DataManager.Instance == null)
            {
                Debug.LogWarning("[PauseMenuController] DataManager instance is missing. Volume was not saved.");
                return;
            }

            DataManager.Instance.SetFloat(DataManager.Keys.MasterVolume, volume);
        }

        private void OnReturnToMainMenuClicked()
        {
            CloseSelf();

            if (GameSceneManager.Instance == null)
            {
                Debug.LogError("[PauseMenuController] GameSceneManager instance is missing.", this);
                return;
            }

            GameSceneManager.Instance.LoadSceneById(StartSceneId);
        }

        private void OnCloseClicked()
        {
            CloseSelf();
        }

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

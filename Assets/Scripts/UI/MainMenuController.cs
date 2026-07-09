using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BokeGameJam.UI
{
    /// <summary>
    /// Handles the StartScene main menu button events.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private const string SelectSceneId = "Level1";

        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private ResourceDefinitionDatabase.UIResource settingsPanelResource;

        private void Awake()
        {
            BindButton(startButton, OnStartGameClicked);
            BindButton(settingsButton, OnSettingsClicked);
            BindButton(quitButton, OnQuitGameClicked);
        }

        private void BindButton(Button button, UnityAction callback)
        {
            if (button == null)
            {
                Debug.LogWarning("[MainMenuController] Button reference is missing.", this);
                return;
            }

            button.onClick.RemoveListener(callback);
            button.onClick.AddListener(callback);
        }

        public void OnStartGameClicked()
        {
            if (GameSceneManager.Instance == null)
            {
                Debug.LogError("[MainMenuController] GameSceneManager instance is missing.", this);
                return;
            }

            GameSceneManager.Instance.LoadSceneById(SelectSceneId);
        }

        public void OnSettingsClicked()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[MainMenuController] UIManager instance is missing.", this);
                return;
            }

            if (settingsPanelResource != null && !string.IsNullOrWhiteSpace(settingsPanelResource.Id))
            {
                UIManager.Instance.LoadUI(settingsPanelResource);
                return;
            }

            UIManager.Instance.LoadUIById(SettingsPanelController.UiId);
        }

        public void OnQuitGameClicked()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Handles the SampleScene main menu button events.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

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
        }

        public void OnSettingsClicked()
        {
        }

        public void OnQuitGameClicked()
        {
        }
    }
}

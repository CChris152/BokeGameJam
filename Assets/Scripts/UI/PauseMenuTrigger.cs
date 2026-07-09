using UnityEngine;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Opens/closes the pause menu with Escape when enabled.
    /// Toggle <see cref="EscEnabled"/> from other systems to turn this feature on/off.
    /// </summary>
    public class PauseMenuTrigger : MonoBehaviour
    {
        [Header("Escape Trigger")]
        [Tooltip("When false, Escape will not open or close the pause menu.")]
        [SerializeField] private bool escEnabled;

        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        /// <summary>
        /// External systems can enable/disable Escape pause menu behavior.
        /// </summary>
        public bool EscEnabled
        {
            get => escEnabled;
            set => escEnabled = value;
        }

        public void SetEscEnabled(bool enabled)
        {
            escEnabled = enabled;
        }

        private void Update()
        {
            if (!escEnabled)
                return;

            if (!UnityEngine.Input.GetKeyDown(toggleKey))
                return;

            TogglePauseMenu();
        }

        public void TogglePauseMenu()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[PauseMenuTrigger] UIManager instance is missing.", this);
                return;
            }

            if (UIManager.Instance.IsUIVisibleById(PauseMenuController.UiId))
            {
                UIManager.Instance.CloseUIById(PauseMenuController.UiId);
                return;
            }

            UIManager.Instance.LoadUIById(PauseMenuController.UiId);
        }

        public void OpenPauseMenu()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[PauseMenuTrigger] UIManager instance is missing.", this);
                return;
            }

            UIManager.Instance.LoadUIById(PauseMenuController.UiId);
        }

        public void ClosePauseMenu()
        {
            if (UIManager.Instance == null)
                return;

            UIManager.Instance.CloseUIById(PauseMenuController.UiId);
        }
    }
}

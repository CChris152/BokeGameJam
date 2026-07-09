using UnityEngine;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 启用时按 Escape 打开/关闭暂停菜单。
    /// 其他系统可通过切换 <see cref="EscEnabled"/> 开关此功能。
    /// </summary>
    public class PauseMenuTrigger : MonoBehaviour
    {
        [Header("Escape Trigger")]
        [Tooltip("为 false 时，Escape 不会打开或关闭暂停菜单。")]
        [SerializeField] private bool escEnabled;

        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        /// <summary>
        /// 外部系统可启用/禁用 Escape 暂停菜单行为。
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

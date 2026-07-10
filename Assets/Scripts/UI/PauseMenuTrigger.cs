using UnityEngine;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 暂停菜单 ESC 触发器。
    /// 当 <see cref="EscEnabled"/> 为 true 时，按 Escape 可打开/关闭暂停弹窗；
    /// 关卡等外部系统可通过该变量随时启用或关闭此功能。
    /// </summary>
    public class PauseMenuTrigger : MonoBehaviour
    {
        [Header("ESC 触发")]
        [Tooltip("为 false 时，按 Escape 不会打开或关闭暂停菜单。可由外部代码修改。")]
        [SerializeField] private bool escEnabled;

        [Tooltip("用于开关暂停菜单的按键，默认 Escape")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        /// <summary>
        /// 是否启用 ESC 开关暂停菜单。
        /// 外部系统可直接读写该属性来打开/关闭功能。
        /// </summary>
        public bool EscEnabled
        {
            get => escEnabled;
            set => escEnabled = value;
        }

        /// <summary>设置是否启用 ESC 触发暂停菜单。</summary>
        public void SetEscEnabled(bool enabled)
        {
            escEnabled = enabled;
        }

        private void Update()
        {
            // 功能关闭时不响应按键
            if (!escEnabled)
                return;

            if (!UnityEngine.Input.GetKeyDown(toggleKey))
                return;

            TogglePauseMenu();
        }

        /// <summary>
        /// 切换暂停菜单显示状态：已打开则关闭，未打开则加载。
        /// 未启用 ESC 时不允许新打开（仍可关闭已打开的菜单）。
        /// </summary>
        public void TogglePauseMenu()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[PauseMenuTrigger] UIManager instance is missing.", this);
                return;
            }

            if (UIManager.Instance.IsVisible(PauseMenuController.ResourceId))
            {
                UIManager.Instance.Close(PauseMenuController.ResourceId);
                return;
            }

            if (!escEnabled)
                return;

            UIManager.Instance.Load(PauseMenuController.ResourceId);
        }

        /// <summary>打开暂停菜单（若已打开则由 UIManager 复用已有实例）。未启用 ESC 时忽略。</summary>
        public void OpenPauseMenu()
        {
            if (!escEnabled)
                return;

            if (UIManager.Instance == null)
            {
                Debug.LogError("[PauseMenuTrigger] UIManager instance is missing.", this);
                return;
            }

            UIManager.Instance.Load(PauseMenuController.ResourceId);
        }

        /// <summary>关闭暂停菜单（未打开时无操作）。</summary>
        public void ClosePauseMenu()
        {
            if (UIManager.Instance == null)
                return;

            UIManager.Instance.Close(PauseMenuController.ResourceId);
        }
    }
}

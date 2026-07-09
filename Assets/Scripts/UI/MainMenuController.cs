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
    /// StartScene 主菜单控制器：开始游戏、打开设置、退出游戏。
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>点击「开始游戏」后加载的场景资源 id。</summary>
        private const string SelectSceneId = "Level1";

        [Header("主菜单按钮")]
        [Tooltip("开始游戏按钮")]
        [SerializeField] private Button startButton;

        [Tooltip("打开设置弹窗按钮")]
        [SerializeField] private Button settingsButton;

        [Tooltip("退出游戏按钮")]
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            BindButton(startButton, OnStartGameClicked);
            BindButton(settingsButton, OnSettingsClicked);
            BindButton(quitButton, OnQuitGameClicked);
        }

        /// <summary>为按钮绑定点击回调（先移除再添加，避免重复订阅）。</summary>
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

        /// <summary>开始游戏：切换到关卡场景。</summary>
        public void OnStartGameClicked()
        {
            EventManager.Emit(GameEvents.GameStartRequested);
        }

        /// <summary>打开设置弹窗。</summary>
        public void OnSettingsClicked()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[MainMenuController] UIManager instance is missing.", this);
                return;
            }

            UIManager.Instance.Load(SettingsPanelController.ResourceId);
        }

        /// <summary>退出游戏：编辑器中停止 Play，真机中退出应用。</summary>
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

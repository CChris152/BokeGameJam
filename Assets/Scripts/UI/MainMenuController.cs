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

        /// <summary>
        /// 启动淡出（全黑停留再透明）只在整局第一次进入主菜单时播放；
        /// 之后从暂停等流程回到主菜单时，由加载过渡动画负责揭幕。
        /// </summary>
        private static bool hasPlayedStartupFadeOut;

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

        private void Start()
        {
            // 仅首次启动播放「黑屏停留 → 淡出」；回主菜单时跳过，避免打断加载过渡动画
            if (hasPlayedStartupFadeOut)
                return;

            hasPlayedStartupFadeOut = true;
            if (BlackScreenLoader.Instance != null)
                BlackScreenLoader.Instance.PlayFadeOut();
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

        /// <summary>
        /// 开始游戏：先播黑屏加载动画，淡入到全黑后再广播开局事件。
        /// </summary>
        public void OnStartGameClicked()
        {
            if (BlackScreenLoader.Instance == null)
            {
                Debug.LogWarning("[MainMenuController] BlackScreenLoader missing, start game immediately.", this);
                EventManager.Emit(GameEvents.GameStartRequested);
                return;
            }

            BlackScreenLoader.Instance.PlayLoadingAnimation(() =>
            {
                EventManager.Emit(GameEvents.GameStartRequested);
            });
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

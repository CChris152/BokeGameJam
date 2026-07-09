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
    /// 处理 StartScene 主菜单按钮事件。
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
            EventManager.Emit(GameEvents.GameStartRequested);
        }

        public void OnSettingsClicked()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogError("[MainMenuController] UIManager instance is missing.", this);
                return;
            }

            UIManager.Instance.Load(SettingsPanelController.ResourceId);
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

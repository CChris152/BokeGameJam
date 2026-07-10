using BokeGameJam.Core;
using BokeGameJam.Levels;
using UnityEngine;
using UnityEngine.EventSystems;
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
        /// <summary>主菜单「开始游戏」进入的关卡 id（对应 LevelCatalog）。</summary>
        private const string StartLevelId = "level_1";

        /// <summary>主菜单循环 BGM 资源 id（对应 ResourceDefinitionDatabase）。</summary>
        private const string MainMenuBgmId = "scene1_background";

        /// <summary>点击开始游戏后切换到的关卡 BGM 资源 id。</summary>
        private const string GameplayBgmId = "biao-bgm-Whispered Ruins";

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

        private bool startGameRequested;

        private void Awake()
        {
            BindButton(startButton, OnStartGameClicked);
            BindButton(settingsButton, OnSettingsClicked);
            BindButton(quitButton, OnQuitGameClicked);
        }

        private void Start()
        {
            PlayMainMenuBgm();

            // 仅首次启动播放「黑屏停留 → 淡出」；回主菜单时跳过，避免打断加载过渡动画
            if (hasPlayedStartupFadeOut)
                return;

            hasPlayedStartupFadeOut = true;
            if (BlackScreenLoader.Instance != null)
                BlackScreenLoader.Instance.PlayFadeOut();
        }

        /// <summary>进入主菜单时循环播放背景音乐。</summary>
        private static void PlayMainMenuBgm()
        {
            if (GameAudioManager.Instance == null)
            {
                Debug.LogWarning("[MainMenuController] GameAudioManager instance is missing. Main menu BGM was not started.");
                return;
            }

            // fadeDuration=0：首次进入直接开播；已在播同一首时 PlayBGMById 会直接返回
            GameAudioManager.Instance.PlayBGMById(MainMenuBgmId, 0f);
        }

        /// <summary>开始游戏时切换到关卡 BGM（默认淡出 1.5s → 淡入 1.5s）。</summary>
        private static void SwitchToGameplayBgm()
        {
            if (GameAudioManager.Instance == null)
            {
                Debug.LogWarning("[MainMenuController] GameAudioManager instance is missing. Gameplay BGM was not started.");
                return;
            }

            GameAudioManager.Instance.SwitchBGMById(GameplayBgmId);
        }

        /// <summary>为按钮绑定点击回调（先移除再添加，避免重复订阅）。</summary>
        private void BindButton(Button button, UnityAction callback)
        {
            if (button == null)
            {
                Debug.LogWarning("[MainMenuController] Button reference is missing.", this);
                return;
            }

            button.onClick.RemoveListener(PlayUiConfirm);
            button.onClick.RemoveListener(callback);
            button.onClick.AddListener(PlayUiConfirm);
            button.onClick.AddListener(callback);
        }

        private static void PlayUiConfirm()
        {
            if (GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.UiConfirm);
        }

        /// <summary>
        /// 开始游戏：播放开场媒体序列，完成后加载关卡 1（LevelCatalog: level_1 → Level1）。
        /// </summary>
        public void OnStartGameClicked()
        {
            // Unity 默认 Input 的 Submit 绑定了 Space/Enter。鼠标点过按钮后它仍保持选中，
            // 再按空格会再次触发 onClick，导致开场 CG 被 StopCurrent 后重播。
            if (startGameRequested)
                return;

            startGameRequested = true;
            SetMenuButtonsInteractable(false);
            ClearUiSelection();

            SwitchToGameplayBgm();

            BlackScreenMediaPlayer mediaPlayer = BlackScreenMediaPlayer.Instance
                ?? BlackScreenMediaPlayer.EnsureExists();

            if (mediaPlayer == null)
            {
                Debug.LogWarning("[MainMenuController] BlackScreenMediaPlayer missing, start game immediately.", this);
                StartLevel1();
                return;
            }

            mediaPlayer.Play(BlackScreenMediaPlayer.PresetStartToLevel1, StartLevel1);
        }

        /// <summary>禁用主菜单按钮，避免 Submit（空格）重复触发。</summary>
        private void SetMenuButtonsInteractable(bool interactable)
        {
            if (startButton != null)
                startButton.interactable = interactable;
            if (settingsButton != null)
                settingsButton.interactable = interactable;
            if (quitButton != null)
                quitButton.interactable = interactable;
        }

        /// <summary>清除 EventSystem 当前选中，避免键盘 Submit 打到已点过的按钮。</summary>
        private static void ClearUiSelection()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>确保管理器在场后，按 Catalog 加载第一关。</summary>
        private static void StartLevel1()
        {
            GameManager.EnsureExists();
            LevelManager manager = LevelManager.EnsureExists();
            if (manager == null)
            {
                Debug.LogError("[MainMenuController] LevelManager missing, cannot start Level1.");
                return;
            }

            manager.LoadLevelById(StartLevelId);
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

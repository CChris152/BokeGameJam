using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// Small runtime smoke test for the project template.
    /// </summary>
    public class TemplateDemoController : MonoBehaviour
    {
        [Header("Resource Ids")]
        [SerializeField] private string imageId = "template_image";
        [SerializeField] private string bgmId = "template_bgm";
        [SerializeField] private string sfxId = "template_click";
        [SerializeField] private string targetSceneId = "template_gallery";

        [Header("UI")]
        [SerializeField] private Image previewImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button playSfxButton;
        [SerializeField] private Button switchSceneButton;
        [SerializeField] private Button toggleBgmButton;

        private bool bgmMuted;

        private void Awake()
        {
            if (playSfxButton != null)
                playSfxButton.onClick.AddListener(PlaySfx);

            if (switchSceneButton != null)
                switchSceneButton.onClick.AddListener(SwitchScene);

            if (toggleBgmButton != null)
                toggleBgmButton.onClick.AddListener(ToggleBgm);
        }

        private void Start()
        {
            ShowTemplateImage();
            PlayBgm();
            SetStatus("Template ready. Image loaded, BGM started.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                PlaySfx();

            if (Input.GetKeyDown(KeyCode.Alpha2))
                ToggleBgm();

            if (Input.GetKeyDown(KeyCode.Space))
                SwitchScene();
        }

        private void ShowTemplateImage()
        {
            if (previewImage == null)
            {
                Debug.LogWarning("[TemplateDemoController] Preview image is not assigned.");
                return;
            }

            Sprite sprite = ResourcesManager.LoadSpriteById(imageId);
            if (sprite == null)
            {
                SetStatus($"Image failed: {imageId}");
                return;
            }

            previewImage.sprite = sprite;
            previewImage.preserveAspect = true;
        }

        private void PlayBgm()
        {
            if (GameAudioManager.Instance == null)
            {
                SetStatus("Audio manager missing.");
                return;
            }

            GameAudioManager.Instance.PlayBGMById(bgmId, 0.25f);
        }

        private void PlaySfx()
        {
            if (GameAudioManager.Instance == null)
            {
                SetStatus("Audio manager missing.");
                return;
            }

            GameAudioManager.Instance.PlaySFXById(sfxId);
            SetStatus($"Played SFX: {sfxId}");
        }

        private void ToggleBgm()
        {
            if (GameAudioManager.Instance == null)
            {
                SetStatus("Audio manager missing.");
                return;
            }

            bgmMuted = !bgmMuted;
            GameAudioManager.Instance.SetBGMVolume(bgmMuted ? 0f : 0.6f);
            SetStatus(bgmMuted ? "BGM muted." : "BGM volume restored.");
        }

        private void SwitchScene()
        {
            if (GameSceneManager.Instance == null)
            {
                SetStatus("Scene manager missing.");
                return;
            }

            SetStatus($"Loading scene: {targetSceneId}");
            GameSceneManager.Instance.LoadSceneById(targetSceneId);
        }

        private void SetStatus(string message)
        {
            Debug.Log($"[TemplateDemoController] {message}");

            if (statusText != null)
                statusText.text = message;

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = "BokeGameJam Template";
        }
    }
}

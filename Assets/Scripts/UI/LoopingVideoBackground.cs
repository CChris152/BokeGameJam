using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 主菜单等全屏 UI 背景：用 VideoPlayer 循环播放视频到 RawImage。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(VideoPlayer))]
    public class LoopingVideoBackground : MonoBehaviour
    {
        [Header("视频")]
        [Tooltip("循环播放的视频资源")]
        [SerializeField] private VideoClip videoClip;

        [Tooltip("是否静音（主菜单背景通常静音，避免和 BGM 冲突）")]
        [SerializeField] private bool muteAudio = true;

        [Tooltip("进入场景后自动播放")]
        [SerializeField] private bool playOnStart = true;

        private RawImage rawImage;
        private VideoPlayer videoPlayer;
        private RenderTexture renderTexture;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            videoPlayer = GetComponent<VideoPlayer>();
            ConfigurePlayer();
        }

        private void Start()
        {
            if (playOnStart)
                Play();
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnPrepareCompleted;
                if (videoPlayer.isPlaying)
                    videoPlayer.Stop();
            }

            if (rawImage != null)
                rawImage.texture = null;

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }
        }

        /// <summary>准备并开始循环播放。</summary>
        public void Play()
        {
            if (videoClip == null)
            {
                Debug.LogWarning("[LoopingVideoBackground] VideoClip is missing.", this);
                return;
            }

            if (videoPlayer.clip != videoClip)
                videoPlayer.clip = videoClip;

            EnsureRenderTexture();

            if (videoPlayer.isPrepared)
            {
                videoPlayer.Play();
                return;
            }

            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            videoPlayer.prepareCompleted += OnPrepareCompleted;
            videoPlayer.Prepare();
        }

        public void Stop()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
                videoPlayer.Stop();
        }

        private void ConfigurePlayer()
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = muteAudio
                ? VideoAudioOutputMode.None
                : VideoAudioOutputMode.Direct;
            videoPlayer.source = VideoSource.VideoClip;

            if (videoClip != null)
                videoPlayer.clip = videoClip;
        }

        private void EnsureRenderTexture()
        {
            int width = videoClip != null ? (int)videoClip.width : 1280;
            int height = videoClip != null ? (int)videoClip.height : 720;
            if (width <= 0) width = 1280;
            if (height <= 0) height = 720;

            if (renderTexture != null &&
                renderTexture.width == width &&
                renderTexture.height == height)
            {
                videoPlayer.targetTexture = renderTexture;
                rawImage.texture = renderTexture;
                return;
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }

            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "LoopingVideoBackgroundRT",
                filterMode = FilterMode.Bilinear
            };
            renderTexture.Create();

            videoPlayer.targetTexture = renderTexture;
            rawImage.texture = renderTexture;
            rawImage.color = Color.white;
        }

        private void OnPrepareCompleted(VideoPlayer source)
        {
            source.prepareCompleted -= OnPrepareCompleted;
            source.Play();
        }
    }
}

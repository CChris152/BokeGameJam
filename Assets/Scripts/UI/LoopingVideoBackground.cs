using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 主菜单等全屏 UI 背景：双 VideoPlayer 交替播放，规避单实例 isLooping 在循环点的解码卡顿。
    /// 不修改视频资源；要求素材首尾帧一致时，视觉上可接近无缝。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(VideoPlayer))]
    public class LoopingVideoBackground : MonoBehaviour
    {
        [Header("视频")]
        [Tooltip("循环播放的视频资源（建议首尾帧一致）")]
        [SerializeField] private VideoClip videoClip;

        [Tooltip("是否静音（主菜单背景通常静音，避免和 BGM 冲突）")]
        [SerializeField] private bool muteAudio = true;

        [Tooltip("进入场景后自动播放")]
        [SerializeField] private bool playOnStart = true;

        private RawImage rawImage;
        private VideoPlayer[] players;
        private RenderTexture[] renderTextures;
        private int activeIndex;
        private bool hasStarted;
        private bool isSwitching;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            EnsurePlayers();
            ConfigureAllPlayers();
        }

        private void OnEnable()
        {
            EnsurePlayers();
            BindPlayerEvents(true);
        }

        private void Start()
        {
            if (playOnStart)
                Play();
        }

        private void OnDisable()
        {
            BindPlayerEvents(false);
        }

        private void OnDestroy()
        {
            BindPlayerEvents(false);
            StopAllPlayers();
            ReleaseRenderTextures();

            if (rawImage != null)
                rawImage.texture = null;
        }

        /// <summary>准备并开始无缝循环播放。</summary>
        public void Play()
        {
            if (videoClip == null)
            {
                Debug.LogWarning("[LoopingVideoBackground] VideoClip is missing.", this);
                return;
            }

            EnsurePlayers();
            ConfigureAllPlayers();
            EnsureRenderTextures();

            activeIndex = 0;
            hasStarted = false;
            isSwitching = false;

            // 先准备 A；A 开播后再准备 B，保证循环点时 B 已就绪
            players[0].Prepare();
        }

        public void Stop()
        {
            hasStarted = false;
            isSwitching = false;
            StopAllPlayers();
        }

        private void EnsurePlayers()
        {
            if (players != null && players.Length == 2 && players[0] != null && players[1] != null)
                return;

            players = new VideoPlayer[2];
            players[0] = GetComponent<VideoPlayer>();
            if (players[0] == null)
                players[0] = gameObject.AddComponent<VideoPlayer>();

            // 第二个播放器专用于接缝，避免单实例循环点重新 seek 造成卡顿
            var existing = GetComponents<VideoPlayer>();
            players[1] = existing.Length > 1 ? existing[1] : gameObject.AddComponent<VideoPlayer>();
        }

        private void ConfigureAllPlayers()
        {
            for (int i = 0; i < players.Length; i++)
                ConfigurePlayer(players[i]);
        }

        private void ConfigurePlayer(VideoPlayer player)
        {
            player.playOnAwake = false;
            // 关键关键内置循环：改由双播放器手动接缝
            player.isLooping = false;
            player.waitForFirstFrame = true;
            // 关闭跳帧，减少循环点附近因追时钟造成的画面跳动
            player.skipOnDrop = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = muteAudio
                ? VideoAudioOutputMode.None
                : VideoAudioOutputMode.Direct;
            player.source = VideoSource.VideoClip;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.playbackSpeed = 1f;

            if (videoClip != null)
                player.clip = videoClip;
        }

        private void EnsureRenderTextures()
        {
            int width = videoClip != null ? (int)videoClip.width : 1280;
            int height = videoClip != null ? (int)videoClip.height : 720;
            if (width <= 0) width = 1280;
            if (height <= 0) height = 720;

            if (renderTextures == null)
                renderTextures = new RenderTexture[2];

            for (int i = 0; i < 2; i++)
            {
                if (renderTextures[i] != null &&
                    renderTextures[i].width == width &&
                    renderTextures[i].height == height)
                {
                    players[i].targetTexture = renderTextures[i];
                    continue;
                }

                if (renderTextures[i] != null)
                {
                    renderTextures[i].Release();
                    Destroy(renderTextures[i]);
                }

                renderTextures[i] = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "LoopingVideoBackgroundRT_" + i,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                renderTextures[i].Create();
                players[i].targetTexture = renderTextures[i];
            }

            rawImage.texture = renderTextures[0];
            rawImage.color = Color.white;
        }

        private void BindPlayerEvents(bool bind)
        {
            if (players == null)
                return;

            for (int i = 0; i < players.Length; i++)
            {
                VideoPlayer player = players[i];
                if (player == null)
                    continue;

                player.prepareCompleted -= OnPrepareCompleted;
                player.loopPointReached -= OnLoopPointReached;
                player.errorReceived -= OnErrorReceived;

                if (bind)
                {
                    player.prepareCompleted += OnPrepareCompleted;
                    player.loopPointReached += OnLoopPointReached;
                    player.errorReceived += OnErrorReceived;
                }
            }
        }

        private void OnPrepareCompleted(VideoPlayer source)
        {
            int index = IndexOf(source);
            if (index < 0)
                return;

            // 首次：A 准备好后开播，并立刻准备 B
            if (!hasStarted && index == 0)
            {
                hasStarted = true;
                rawImage.texture = renderTextures[0];
                source.Play();
                players[1].Prepare();
                return;
            }

            // 非激活播放器准备完成即可，等待接缝时切换
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            int finishedIndex = IndexOf(source);
            if (finishedIndex < 0 || finishedIndex != activeIndex || isSwitching)
                return;

            int nextIndex = 1 - activeIndex;
            VideoPlayer next = players[nextIndex];

            if (!next.isPrepared)
            {
                // 兜底：B 尚未就绪时仍尽量续播，避免黑屏
                Debug.LogWarning("[LoopingVideoBackground] Standby player not prepared at loop point; restarting active player.", this);
                source.time = 0.0;
                source.Play();
                next.Prepare();
                return;
            }

            isSwitching = true;

            // 先切到已准备好的下一轨画面，再停掉刚结束的播放器
            rawImage.texture = renderTextures[nextIndex];
            next.time = 0.0;
            next.Play();

            source.Stop();
            activeIndex = nextIndex;
            isSwitching = false;

            // 让刚结束的播放器重新 Prepare，供下一轮接缝使用
            source.Prepare();
        }

        private void OnErrorReceived(VideoPlayer source, string message)
        {
            Debug.LogError($"[LoopingVideoBackground] VideoPlayer error: {message}", this);
        }

        private int IndexOf(VideoPlayer player)
        {
            if (players == null)
                return -1;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == player)
                    return i;
            }

            return -1;
        }

        private void StopAllPlayers()
        {
            if (players == null)
                return;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].isPlaying)
                    players[i].Stop();
            }
        }

        private void ReleaseRenderTextures()
        {
            if (renderTextures == null)
                return;

            for (int i = 0; i < renderTextures.Length; i++)
            {
                if (renderTextures[i] == null)
                    continue;

                renderTextures[i].Release();
                Destroy(renderTextures[i]);
                renderTextures[i] = null;
            }
        }
    }
}

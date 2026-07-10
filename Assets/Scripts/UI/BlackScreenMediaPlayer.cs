using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BokeGameJam.UI
{
    public enum MediaSequenceKind
    {
        Text = 0,
        Video = 1
    }

    /// <summary>播放序列中的一项：类型 + 配置表中的序号。</summary>
    [Serializable]
    public struct MediaSequenceItem
    {
        public MediaSequenceKind Kind;
        public int Id;

        public MediaSequenceItem(MediaSequenceKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public static MediaSequenceItem Text(int id) => new(MediaSequenceKind.Text, id);

        public static MediaSequenceItem Video(int id) => new(MediaSequenceKind.Video, id);
    }

    /// <summary>
    /// 全屏黑屏媒体序列播放器（单例，跨场景不销毁）。
    /// 默认透明；可在 Inspector 配置多条带序号的文本与视频。
    /// 播放流程：黑屏淡入 → 按序播放条目（条目间 1s 淡入淡出）→ 回调 → 黑屏淡出。
    /// 黑屏时长默认对齐 <see cref="BlackScreenLoader"/>。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class BlackScreenMediaPlayer : MonoBehaviour
    {
        [Serializable]
        public class TextEntry
        {
            [Tooltip("文本序号，供 Play 接口引用")]
            public int id;

            [TextArea(2, 8)]
            public string text;
        }

        [Serializable]
        public class VideoEntry
        {
            [Tooltip("视频序号，供 Play 接口引用")]
            public int id;

            public VideoClip clip;
        }

        /// <summary>命名播放预设：一组按顺序的文字/视频步骤。</summary>
        [Serializable]
        public class SequencePreset
        {
            [Tooltip("预设 id，调用 Play(presetId) 时使用")]
            public string id;

            [Tooltip("按顺序播放的步骤")]
            public List<MediaSequenceItem> steps = new();
        }

        /// <summary>开始游戏进入第一关的预设 id。</summary>
        public const string PresetStartToLevel1 = "StartToLevel1";

        /// <summary>第一关通关后进入第二关的预设 id。</summary>
        public const string PresetStartToLevel2 = "StartToLevel2";

        public static BlackScreenMediaPlayer Instance { get; private set; }

        [Header("遮罩")]
        [SerializeField] private Image blackImage;
        [SerializeField] private int sortingOrder = 1100;

        [Header("内容显示")]
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private VideoPlayer videoPlayer;

        [Header("字体")]
        [Tooltip("字幕 TMP 字体；留空则从 Resources 加载默认路径。")]
        [SerializeField] private TMP_FontAsset messageFont;
        [SerializeField] private string messageFontResourcePath = "Art/Fonts/FZFENGRSTJW-EB SDF";
        [SerializeField] private float messageFontSize = 48f;

        [Header("资源配置")]
        [SerializeField] private List<TextEntry> texts = new();
        [SerializeField] private List<VideoEntry> videos = new();

        [Header("播放预设")]
        [Tooltip("可配置多套播放序列；调用时只传预设 id。")]
        [SerializeField] private List<SequencePreset> presets = new()
        {
            new SequencePreset
            {
                id = PresetStartToLevel1,
                steps = new List<MediaSequenceItem>
                {
                    MediaSequenceItem.Video(0),
                    MediaSequenceItem.Text(0),
                }
            },
            new SequencePreset
            {
                id = PresetStartToLevel2,
                steps = new List<MediaSequenceItem>
                {
                    MediaSequenceItem.Text(1),
                    MediaSequenceItem.Text(2),
                    MediaSequenceItem.Text(3),
                    MediaSequenceItem.Video(1),
                    MediaSequenceItem.Text(4),
                }
            }
        };

        [Header("时序")]
        [Tooltip("文字停留时长（秒）")]
        [SerializeField] private float textHoldDuration = 3f;

        [Tooltip("相邻条目（文字/视频）淡入淡出时长（秒）")]
        [SerializeField] private float contentFadeDuration = 1f;

        [Tooltip("若未找到 BlackScreenLoader，使用的黑屏淡入时长")]
        [SerializeField] private float fallbackBlackFadeInDuration = 1f;

        [Tooltip("若未找到 BlackScreenLoader，使用的黑屏淡出时长")]
        [SerializeField] private float fallbackBlackFadeOutDuration = 1.5f;

        [Header("视频渲染")]
        [SerializeField] private int videoRenderWidth = 1920;
        [SerializeField] private int videoRenderHeight = 1080;

        private Canvas canvas;
        private CanvasGroup rootGroup;
        private Coroutine runningRoutine;
        private RenderTexture videoRenderTexture;
        private bool videoFinished;

        public bool IsPlaying => runningRoutine != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureComponents();
            SetRootAlpha(0f);
            SetContentAlpha(0f);
            SetBlocksRaycasts(false);
            HideContentVisuals();
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
                videoPlayer.loopPointReached -= OnVideoLoopPointReached;

            if (videoRenderTexture != null)
            {
                videoRenderTexture.Release();
                Destroy(videoRenderTexture);
                videoRenderTexture = null;
            }

            if (Instance == this)
                Instance = null;
        }

        /// <summary>确保场景中存在单例；优先复用已有实例，避免新建空配置对象。</summary>
        public static BlackScreenMediaPlayer EnsureExists()
        {
            if (Instance != null)
                return Instance;

            BlackScreenMediaPlayer existing = UnityEngine.Object.FindObjectOfType<BlackScreenMediaPlayer>(true);
            if (existing != null)
                return existing;

            Debug.LogError(
                "[BlackScreenMediaPlayer] 场景中不存在实例。请把 Prefabs/Manager/BlackScreenMediaPlayer 放到 StartScene。");
            return null;
        }

        /// <summary>
        /// 按预设 id 播放配置好的序列，播完后调用 <paramref name="onComplete"/>，再黑屏淡出。
        /// </summary>
        public void Play(string presetId, Action onComplete = null)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                Debug.LogWarning("[BlackScreenMediaPlayer] presetId 为空。", this);
                onComplete?.Invoke();
                return;
            }

            if (!TryGetPreset(presetId.Trim(), out SequencePreset preset))
            {
                Debug.LogWarning($"[BlackScreenMediaPlayer] 找不到预设 '{presetId}'。", this);
                onComplete?.Invoke();
                return;
            }

            Play(preset.steps, onComplete);
        }

        /// <summary>
        /// 按文本/视频序号列表播放。
        /// 交错规则：按索引 i=0,1,2... 先播 textIds[i]（若有），再播 videoIds[i]（若有），可交替。
        /// </summary>
        public void Play(IReadOnlyList<int> textIds, IReadOnlyList<int> videoIds, Action onComplete = null)
        {
            List<MediaSequenceItem> sequence = BuildInterleavedSequence(textIds, videoIds);
            Play(sequence, onComplete);
        }

        /// <summary>按自定义顺序播放（可任意交替文字与视频）。</summary>
        public void Play(IReadOnlyList<MediaSequenceItem> sequence, Action onComplete = null)
        {
            StopCurrent();
            runningRoutine = StartCoroutine(PlayRoutine(sequence, onComplete));
        }

        private bool TryGetPreset(string presetId, out SequencePreset preset)
        {
            preset = null;
            if (presets == null)
                return false;

            for (int i = 0; i < presets.Count; i++)
            {
                SequencePreset entry = presets[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                    continue;

                if (!string.Equals(entry.id.Trim(), presetId, StringComparison.Ordinal))
                    continue;

                preset = entry;
                return true;
            }

            return false;
        }

        /// <summary>停止当前播放，保持当前透明度。</summary>
        public void StopCurrent()
        {
            if (runningRoutine != null)
            {
                StopCoroutine(runningRoutine);
                runningRoutine = null;
            }

            StopVideoPlayback();
        }

        private static List<MediaSequenceItem> BuildInterleavedSequence(
            IReadOnlyList<int> textIds,
            IReadOnlyList<int> videoIds)
        {
            int textCount = textIds != null ? textIds.Count : 0;
            int videoCount = videoIds != null ? videoIds.Count : 0;
            int n = Mathf.Max(textCount, videoCount);
            List<MediaSequenceItem> sequence = new(textCount + videoCount);

            for (int i = 0; i < n; i++)
            {
                if (i < textCount)
                    sequence.Add(MediaSequenceItem.Text(textIds[i]));
                if (i < videoCount)
                    sequence.Add(MediaSequenceItem.Video(videoIds[i]));
            }

            return sequence;
        }

        private IEnumerator PlayRoutine(IReadOnlyList<MediaSequenceItem> sequence, Action onComplete)
        {
            EnsureComponents();
            SetBlocksRaycasts(true);

            float blackIn = ResolveBlackFadeInDuration();
            float blackOut = ResolveBlackFadeOutDuration();
            float contentFade = Mathf.Max(0f, contentFadeDuration);

            SetContentAlpha(0f);
            HideContentVisuals();
            yield return FadeRootAlpha(0f, 1f, blackIn);

            if (sequence != null)
            {
                for (int i = 0; i < sequence.Count; i++)
                {
                    MediaSequenceItem step = sequence[i];
                    if (step.Kind == MediaSequenceKind.Text)
                        yield return PlayTextStep(step.Id, contentFade);
                    else
                        yield return PlayVideoStep(step.Id, contentFade);
                }
            }

            SetContentAlpha(0f);
            HideContentVisuals();
            StopVideoPlayback();

            onComplete?.Invoke();

            yield return FadeRootAlpha(1f, 0f, blackOut);

            SetBlocksRaycasts(false);
            runningRoutine = null;
        }

        private IEnumerator PlayTextStep(int id, float contentFade)
        {
            if (!TryGetText(id, out string body))
            {
                Debug.LogWarning($"[BlackScreenMediaPlayer] 找不到文本序号 {id}。", this);
                yield break;
            }

            StopVideoPlayback();
            if (videoImage != null)
                videoImage.enabled = false;

            if (messageLabel != null)
            {
                messageLabel.gameObject.SetActive(true);
                messageLabel.text = body ?? string.Empty;
            }

            yield return FadeContentAlpha(0f, 1f, contentFade);
            yield return WaitUnscaled(Mathf.Max(0f, textHoldDuration));
            yield return FadeContentAlpha(1f, 0f, contentFade);

            if (messageLabel != null)
                messageLabel.gameObject.SetActive(false);
        }

        private IEnumerator PlayVideoStep(int id, float contentFade)
        {
            if (!TryGetVideo(id, out VideoClip clip))
            {
                Debug.LogWarning($"[BlackScreenMediaPlayer] 找不到视频序号 {id}。", this);
                yield break;
            }

            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
                messageLabel.gameObject.SetActive(false);
            }

            EnsureVideoRenderTarget();
            if (videoImage != null)
            {
                videoImage.enabled = true;
                videoImage.texture = videoRenderTexture;
            }

            if (videoPlayer == null)
            {
                Debug.LogError("[BlackScreenMediaPlayer] VideoPlayer 缺失。", this);
                yield break;
            }

            videoFinished = false;
            videoPlayer.Stop();
            videoPlayer.clip = clip;
            videoPlayer.isLooping = false;
            videoPlayer.Prepare();

            while (!videoPlayer.isPrepared)
                yield return null;

            videoPlayer.Play();
            yield return FadeContentAlpha(0f, 1f, contentFade);

            while (videoPlayer.isPlaying && !videoFinished)
                yield return null;

            yield return FadeContentAlpha(1f, 0f, contentFade);
            StopVideoPlayback();

            if (videoImage != null)
                videoImage.enabled = false;
        }

        private bool TryGetText(int id, out string text)
        {
            text = null;
            if (texts == null)
                return false;

            for (int i = 0; i < texts.Count; i++)
            {
                TextEntry entry = texts[i];
                if (entry == null || entry.id != id)
                    continue;

                text = entry.text;
                return true;
            }

            return false;
        }

        private bool TryGetVideo(int id, out VideoClip clip)
        {
            clip = null;
            if (videos == null)
                return false;

            for (int i = 0; i < videos.Count; i++)
            {
                VideoEntry entry = videos[i];
                if (entry == null || entry.id != id)
                    continue;

                clip = entry.clip;
                return clip != null;
            }

            return false;
        }

        private float ResolveBlackFadeInDuration()
        {
            if (BlackScreenLoader.Instance != null)
                return Mathf.Max(0f, BlackScreenLoader.Instance.LoadingFadeInDuration);
            return Mathf.Max(0f, fallbackBlackFadeInDuration);
        }

        private float ResolveBlackFadeOutDuration()
        {
            if (BlackScreenLoader.Instance != null)
                return Mathf.Max(0f, BlackScreenLoader.Instance.LoadingFadeOutDuration);
            return Mathf.Max(0f, fallbackBlackFadeOutDuration);
        }

        private void OnVideoLoopPointReached(VideoPlayer source)
        {
            videoFinished = true;
        }

        private void StopVideoPlayback()
        {
            if (videoPlayer == null)
                return;

            if (videoPlayer.isPlaying)
                videoPlayer.Stop();
        }

        private void HideContentVisuals()
        {
            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
                messageLabel.gameObject.SetActive(false);
            }

            if (videoImage != null)
                videoImage.enabled = false;
        }

        private IEnumerator FadeRootAlpha(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetRootAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            SetRootAlpha(from);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetRootAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetRootAlpha(to);
        }

        private IEnumerator FadeContentAlpha(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetContentAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            SetContentAlpha(from);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetContentAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetContentAlpha(to);
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            if (duration <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetRootAlpha(float alpha)
        {
            EnsureComponents();
            rootGroup.alpha = Mathf.Clamp01(alpha);

            if (blackImage != null)
            {
                Color c = blackImage.color;
                c.a = 1f;
                blackImage.color = c;
            }
        }

        private void SetContentAlpha(float alpha)
        {
            EnsureComponents();
            if (contentGroup != null)
                contentGroup.alpha = Mathf.Clamp01(alpha);
        }

        private void SetBlocksRaycasts(bool blocks)
        {
            EnsureComponents();
            rootGroup.blocksRaycasts = blocks;
            rootGroup.interactable = blocks;
        }

        private void EnsureVideoRenderTarget()
        {
            int w = Mathf.Max(16, videoRenderWidth);
            int h = Mathf.Max(16, videoRenderHeight);

            if (videoRenderTexture != null
                && (videoRenderTexture.width != w || videoRenderTexture.height != h))
            {
                videoRenderTexture.Release();
                Destroy(videoRenderTexture);
                videoRenderTexture = null;
            }

            if (videoRenderTexture == null)
                videoRenderTexture = new RenderTexture(w, h, 0);

            if (videoPlayer != null)
            {
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.targetTexture = videoRenderTexture;
            }

            if (videoImage != null)
                videoImage.texture = videoRenderTexture;
        }

        private void EnsureComponents()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            canvas.sortingOrder = sortingOrder;
            canvas.overrideSorting = true;

            if (GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            if (rootGroup == null)
                rootGroup = GetComponent<CanvasGroup>();
            if (rootGroup == null)
                rootGroup = gameObject.AddComponent<CanvasGroup>();

            if (blackImage == null)
            {
                Transform child = transform.Find("BlackOverlay");
                if (child != null)
                    blackImage = child.GetComponent<Image>();
            }

            if (blackImage == null)
            {
                GameObject overlay = new("BlackOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay.transform.SetParent(transform, false);
                RectTransform rect = overlay.GetComponent<RectTransform>();
                StretchFull(rect);
                blackImage = overlay.GetComponent<Image>();
                blackImage.color = Color.black;
                blackImage.raycastTarget = true;
            }

            if (contentGroup == null)
            {
                Transform content = transform.Find("Content");
                if (content != null)
                    contentGroup = content.GetComponent<CanvasGroup>();
            }

            if (contentGroup == null)
            {
                GameObject contentGo = new("Content", typeof(RectTransform), typeof(CanvasGroup));
                contentGo.transform.SetParent(transform, false);
                StretchFull(contentGo.GetComponent<RectTransform>());
                contentGroup = contentGo.GetComponent<CanvasGroup>();
            }

            Transform contentRoot = contentGroup.transform;

            if (messageLabel == null)
            {
                Transform label = contentRoot.Find("Message");
                if (label != null)
                    messageLabel = label.GetComponent<TMP_Text>();
            }

            if (messageLabel == null)
            {
                GameObject labelGo = new("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(contentRoot, false);
                RectTransform labelRect = labelGo.GetComponent<RectTransform>();
                StretchFull(labelRect);
                labelRect.offsetMin = new Vector2(80f, 80f);
                labelRect.offsetMax = new Vector2(-80f, -80f);

                messageLabel = labelGo.GetComponent<TextMeshProUGUI>();
                messageLabel.text = string.Empty;
                messageLabel.alignment = TextAlignmentOptions.Center;
                messageLabel.color = Color.white;
                messageLabel.enableWordWrapping = true;
                messageLabel.raycastTarget = false;
                labelGo.SetActive(false);
            }

            ApplyMessageFont();

            if (videoImage == null)
            {
                Transform videoTf = contentRoot.Find("Video");
                if (videoTf != null)
                    videoImage = videoTf.GetComponent<RawImage>();
            }

            if (videoImage == null)
            {
                GameObject videoGo = new("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                videoGo.transform.SetParent(contentRoot, false);
                StretchFull(videoGo.GetComponent<RectTransform>());
                videoImage = videoGo.GetComponent<RawImage>();
                videoImage.color = Color.white;
                videoImage.raycastTarget = false;
                videoImage.enabled = false;
            }

            if (videoPlayer == null)
                videoPlayer = GetComponent<VideoPlayer>();

            if (videoPlayer == null)
                videoPlayer = gameObject.AddComponent<VideoPlayer>();

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.skipOnDrop = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.loopPointReached += OnVideoLoopPointReached;

            contentRoot.SetAsLastSibling();
        }

        private void ApplyMessageFont()
        {
            if (messageLabel == null)
                return;

            TMP_FontAsset font = messageFont;
            if (font == null && !string.IsNullOrWhiteSpace(messageFontResourcePath))
                font = Resources.Load<TMP_FontAsset>(messageFontResourcePath.Trim());

            if (font != null)
            {
                messageFont = font;
                messageLabel.font = font;
            }
            else
            {
                Debug.LogWarning(
                    $"[BlackScreenMediaPlayer] 未找到字体 '{messageFontResourcePath}'。",
                    this);
            }

            messageLabel.fontSize = Mathf.Max(1f, messageFontSize);
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }
    }
}

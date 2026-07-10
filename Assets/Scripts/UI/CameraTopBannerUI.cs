using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 屏幕上方提示条：半透明黑色扁椭圆底 + 白色 TMP 文本。
    /// 默认整体透明；通过 <see cref="PlayStory"/> 播放剧情字幕序列。
    /// </summary>
    public sealed class CameraTopBannerUI : MonoBehaviour
    {
        public const string ResourceId = "CameraTopBanner";

        public static CameraTopBannerUI Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Story Timing")]
        [SerializeField] private float interruptFadeOutDuration = 0.5f;
        [SerializeField] private float storyFadeInDuration = 1.0f;
        [SerializeField] private float lineHoldDuration = 1.5f;
        [SerializeField] private float textFadeOutDuration = 0.5f;
        [SerializeField] private float textFadeInDuration = 0.5f;
        [SerializeField] private float storyFadeOutDuration = 1.0f;

        private Coroutine storyRoutine;
        private Color labelBaseColor = Color.white;
        private bool isPlayingStory;

        /// <summary>当前是否正在播放剧情。</summary>
        public bool IsPlayingStory => isPlayingStory;

        private void Awake()
        {
            Instance = this;
            ResolveRefs();

            if (messageLabel != null)
            {
                labelBaseColor = messageLabel.color;
                labelBaseColor.a = 1f;
                messageLabel.color = labelBaseColor;
                messageLabel.text = string.Empty;
            }

            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public string Text
        {
            get => messageLabel != null ? messageLabel.text : string.Empty;
            set => SetText(value);
        }

        public void SetText(string text)
        {
            if (messageLabel == null)
            {
                Debug.LogWarning("[CameraTopBannerUI] messageLabel 未绑定。", this);
                return;
            }

            messageLabel.text = text ?? string.Empty;
        }

        public void Show(string text = null)
        {
            gameObject.SetActive(true);
            if (text != null)
                SetText(text);
            EnsureCanvasGroup();
            canvasGroup.alpha = 1f;
            SetLabelAlpha(1f);
        }

        public void Hide()
        {
            StopStoryInternal(clearText: true);
            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;
            SetLabelAlpha(1f);
        }

        /// <summary>
        /// 加载并播放剧情字幕。
        /// 若当前正在播放：先整体快速淡出(0.5s)，再淡入(1.0s)后播放新列表。
        /// 若当前未播放：直接淡入(1.0s)后播放。
        /// 每句显示 1.5s → 仅文字淡出 0.5s → 下一句文字淡入 0.5s；全部结束后整体淡出 1.0s。
        /// </summary>
        public void PlayStory(IList<string> lines)
        {
            PlayStory(lines, onComplete: null);
        }

        /// <summary>播放剧情字幕，全部结束后调用 <paramref name="onComplete"/>。</summary>
        public void PlayStory(IList<string> lines, System.Action onComplete)
        {
            if (lines == null || lines.Count == 0)
            {
                Debug.LogWarning("[CameraTopBannerUI] PlayStory 收到空列表。", this);
                onComplete?.Invoke();
                return;
            }

            gameObject.SetActive(true);
            EnsureCanvasGroup();

            List<string> copy = new(lines.Count);
            for (int i = 0; i < lines.Count; i++)
                copy.Add(lines[i] ?? string.Empty);

            if (storyRoutine != null)
                StopCoroutine(storyRoutine);

            storyRoutine = StartCoroutine(PlayStoryRoutine(copy, interruptCurrent: isPlayingStory, onComplete));
        }

        /// <summary>params 重载，便于直接传多句。</summary>
        public void PlayStory(params string[] lines)
        {
            PlayStory((IList<string>)lines, onComplete: null);
        }

        private IEnumerator PlayStoryRoutine(List<string> lines, bool interruptCurrent, System.Action onComplete)
        {
            isPlayingStory = true;

            if (interruptCurrent)
                yield return FadeCanvas(canvasGroup.alpha, 0f, interruptFadeOutDuration);
            else
                canvasGroup.alpha = 0f;

            SetText(lines[0]);
            SetLabelAlpha(1f);
            yield return FadeCanvas(canvasGroup.alpha, 1f, storyFadeInDuration);

            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    SetText(lines[i]);
                    yield return FadeLabel(0f, 1f, textFadeInDuration);
                }

                yield return Wait(lineHoldDuration);

                bool isLast = i >= lines.Count - 1;
                if (!isLast)
                    yield return FadeLabel(1f, 0f, textFadeOutDuration);
            }

            yield return FadeCanvas(canvasGroup.alpha, 0f, storyFadeOutDuration);

            SetText(string.Empty);
            SetLabelAlpha(1f);
            isPlayingStory = false;
            storyRoutine = null;
            onComplete?.Invoke();
        }

        private void StopStoryInternal(bool clearText)
        {
            if (storyRoutine != null)
            {
                StopCoroutine(storyRoutine);
                storyRoutine = null;
            }

            isPlayingStory = false;
            if (clearText)
                SetText(string.Empty);
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            EnsureCanvasGroup();
            float d = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            canvasGroup.alpha = from;

            while (elapsed < d)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / d);
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        private IEnumerator FadeLabel(float from, float to, float duration)
        {
            float d = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            SetLabelAlpha(from);

            while (elapsed < d)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / d);
                SetLabelAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetLabelAlpha(to);
        }

        private static IEnumerator Wait(float duration)
        {
            if (duration <= 0f)
                yield break;
            yield return new WaitForSeconds(duration);
        }

        private void SetLabelAlpha(float alpha)
        {
            if (messageLabel == null)
                return;

            Color c = labelBaseColor;
            c.a = Mathf.Clamp01(alpha);
            messageLabel.color = c;
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void ResolveRefs()
        {
            if (backgroundImage == null)
            {
                Transform bg = transform.Find("Background");
                if (bg != null)
                    backgroundImage = bg.GetComponent<Image>();
            }

            if (messageLabel == null)
            {
                Transform label = transform.Find("Message");
                if (label == null)
                    label = transform.Find("Background/Message");
                if (label != null)
                    messageLabel = label.GetComponent<TMP_Text>();
            }

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}

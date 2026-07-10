using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 全屏黑屏加载遮罩（单例，跨场景不销毁）。
    /// 默认透明；提供加载过渡动画与启动淡出动画，供场景切换等流程调用。
    /// 使用最高层 Overlay Canvas，始终覆盖整个屏幕。
    /// </summary>
    public class BlackScreenLoader : MonoBehaviour
    {
        public static BlackScreenLoader Instance { get; private set; }

        [Header("遮罩")]
        [Tooltip("全屏黑色 Image，通过改 alpha 做淡入淡出")]
        [SerializeField] private Image blackImage;

        [Tooltip("Canvas 排序，需高于普通 UI（UIManager 默认 100）")]
        [SerializeField] private int sortingOrder = 1000;

        [Header("加载动画（透明→不透明→透明）")]
        [Tooltip("透明到全黑的淡入时长（秒）")]
        [SerializeField] private float loadingFadeInDuration = 1f;

        [Tooltip("中间纯黑屏保持时长（秒）")]
        [SerializeField] private float loadingHoldDuration = 0.5f;

        [Tooltip("全黑到透明的淡出时长（秒）")]
        [SerializeField] private float loadingFadeOutDuration = 1.5f;

        /// <summary>加载动画：透明→全黑时长（秒）。</summary>
        public float LoadingFadeInDuration => loadingFadeInDuration;

        /// <summary>加载动画：全黑→透明时长（秒）。</summary>
        public float LoadingFadeOutDuration => loadingFadeOutDuration;

        [Header("启动淡出动画（不透明→透明）")]
        [Tooltip("开始淡出前，先保持全黑的时长（秒）")]
        [SerializeField] private float fadeOutHoldDuration = 2f;

        [Tooltip("全黑到透明的淡出时长（秒）")]
        [SerializeField] private float fadeOutDuration = 1.5f;

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Coroutine runningAnimation;

        /// <summary>当前是否正在播放动画。</summary>
        public bool IsPlaying => runningAnimation != null;

        /// <summary>当前遮罩透明度（0 透明，1 全黑）。</summary>
        public float Alpha => canvasGroup != null ? canvasGroup.alpha : 0f;

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
            SetAlpha(0f);
            SetBlocksRaycasts(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 播放加载动画：透明 → 不透明 → 保持黑屏 → 再透明。
        /// 默认：淡入 1s、黑屏 0.5s、淡出 1.5s。
        /// </summary>
        public void PlayLoadingAnimation()
        {
            PlayLoadingAnimation(loadingFadeInDuration, loadingHoldDuration, loadingFadeOutDuration, null);
        }

        /// <summary>
        /// 播放加载动画；淡入到全黑后立刻调用 <paramref name="onFullyBlack"/>（默认约 1 秒处）。
        /// </summary>
        public void PlayLoadingAnimation(Action onFullyBlack)
        {
            PlayLoadingAnimation(loadingFadeInDuration, loadingHoldDuration, loadingFadeOutDuration, onFullyBlack);
        }

        /// <summary>
        /// 播放加载动画（可自定义淡入、保持、淡出时长）。
        /// </summary>
        public void PlayLoadingAnimation(float fadeInDuration, float holdDuration, float fadeOutDuration, Action onFullyBlack = null)
        {
            StopCurrentAnimation();
            runningAnimation = StartCoroutine(LoadingAnimationRoutine(fadeInDuration, holdDuration, fadeOutDuration, onFullyBlack));
        }

        /// <summary>
        /// 播放启动淡出：先保持全黑，再从不透明变为透明。
        /// 默认：黑屏停留 2s + 淡出 1.5s（全程 3.5s）。
        /// </summary>
        public void PlayFadeOut()
        {
            PlayFadeOut(fadeOutHoldDuration, fadeOutDuration);
        }

        /// <summary>
        /// 播放淡出动画（可自定义黑屏停留与淡出时长）。
        /// </summary>
        public void PlayFadeOut(float holdDuration, float duration)
        {
            StopCurrentAnimation();
            runningAnimation = StartCoroutine(FadeOutRoutine(holdDuration, duration));
        }

        /// <summary>
        /// 播放淡出动画（无黑屏停留，仅淡出）。
        /// </summary>
        public void PlayFadeOut(float duration)
        {
            PlayFadeOut(0f, duration);
        }

        /// <summary>立即设为全黑（不透明）。</summary>
        public void ShowImmediate()
        {
            StopCurrentAnimation();
            SetAlpha(1f);
            SetBlocksRaycasts(true);
        }

        /// <summary>立即设为全透明。</summary>
        public void HideImmediate()
        {
            StopCurrentAnimation();
            SetAlpha(0f);
            SetBlocksRaycasts(false);
        }

        /// <summary>停止当前正在播放的动画（保持当前透明度）。</summary>
        public void StopCurrentAnimation()
        {
            if (runningAnimation == null)
                return;

            StopCoroutine(runningAnimation);
            runningAnimation = null;
        }

        /// <summary>
        /// 加载动画协程：淡入 → 保持 → 淡出。
        /// 淡入结束（全黑）时触发 onFullyBlack。
        /// </summary>
        private IEnumerator LoadingAnimationRoutine(float fadeInDuration, float holdDuration, float fadeOutDuration, Action onFullyBlack)
        {
            fadeInDuration = Mathf.Max(0f, fadeInDuration);
            holdDuration = Mathf.Max(0f, holdDuration);
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

            SetBlocksRaycasts(true);

            // 淡入：透明 → 不透明（默认 1 秒）
            yield return FadeAlpha(0f, 1f, fadeInDuration);

            // 完全黑屏后回调（例如此时再切场景）
            onFullyBlack?.Invoke();

            // 中间黑屏保持
            if (holdDuration > 0f)
                yield return new WaitForSecondsRealtime(holdDuration);

            // 淡出：不透明 → 透明（默认 1.5 秒）
            yield return FadeAlpha(1f, 0f, fadeOutDuration);

            SetBlocksRaycasts(false);
            runningAnimation = null;
        }

        /// <summary>
        /// 淡出协程：先保持全黑，再从不透明到透明。
        /// 默认：停留 2s + 淡出 1.5s = 全程 3.5s。
        /// </summary>
        private IEnumerator FadeOutRoutine(float holdDuration, float duration)
        {
            holdDuration = Mathf.Max(0f, holdDuration);
            duration = Mathf.Max(0.01f, duration);
            SetBlocksRaycasts(true);

            // 确保从全黑开始
            SetAlpha(1f);

            if (holdDuration > 0f)
                yield return new WaitForSecondsRealtime(holdDuration);

            yield return FadeAlpha(1f, 0f, duration);

            SetBlocksRaycasts(false);
            runningAnimation = null;
        }

        /// <summary>在指定时长内将 alpha 从 from 插值到 to（使用不受 timeScale 影响的时间）。</summary>
        private IEnumerator FadeAlpha(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            SetAlpha(from);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            EnsureComponents();
            canvasGroup.alpha = Mathf.Clamp01(alpha);

            if (blackImage != null)
            {
                Color color = blackImage.color;
                color.a = 1f;
                blackImage.color = color;
            }
        }

        private void SetBlocksRaycasts(bool blocks)
        {
            EnsureComponents();
            canvasGroup.blocksRaycasts = blocks;
            canvasGroup.interactable = blocks;
        }

        /// <summary>确保 Canvas / CanvasGroup / 黑色 Image 组件齐全。</summary>
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

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (blackImage == null)
            {
                Transform child = transform.Find("BlackOverlay");
                if (child != null)
                    blackImage = child.GetComponent<Image>();
            }

            if (blackImage == null)
            {
                GameObject overlay = new GameObject("BlackOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay.transform.SetParent(transform, false);

                RectTransform rect = overlay.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                blackImage = overlay.GetComponent<Image>();
                blackImage.color = Color.black;
                blackImage.raycastTarget = true;
            }
        }
    }
}

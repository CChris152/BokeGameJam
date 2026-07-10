using System.Collections;
using System.Collections.Generic;
using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 鼠标悬停时轻微放大 UI 元素，移开后恢复。
    /// 可挂在独立 HitArea 上，通过 <see cref="scaleTarget"/> 缩放外层边框+文字。
    /// 可选：同步对指定 Graphic（Image / TMP）做颜色高亮。
    /// </summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("实际缩放的目标；留空则缩放自身。用于 HitArea 驱动外层按钮缩放。")]
        [SerializeField] private Transform scaleTarget;

        [Tooltip("悬停时的目标缩放倍数")]
        [SerializeField] private float hoverScale = 1.08f;

        [Tooltip("放大/缩小动画时长（秒）")]
        [SerializeField] private float duration = 0.12f;

        [Header("Highlight")]
        [Tooltip("悬停时是否同步改变颜色")]
        [SerializeField] private bool tintOnHover;

        [Tooltip("未悬停时的颜色")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("悬停时的高亮颜色")]
        [SerializeField] private Color hoverColor = new(1f, 0.95f, 0.75f, 1f);

        [Tooltip("需要一起高亮的 Graphic；留空且 tintOnHover 时自动收集 scaleTarget 下的 Graphic")]
        [SerializeField] private Graphic[] tintTargets;

        private Transform resolvedScaleTarget;
        private Vector3 normalScale;
        private Coroutine scaleRoutine;
        private readonly List<Graphic> resolvedTintTargets = new();
        private readonly List<Color> tintFromColors = new();

        private void Awake()
        {
            resolvedScaleTarget = scaleTarget != null ? scaleTarget : transform;
            normalScale = resolvedScaleTarget.localScale;
            ResolveTintTargets();
        }

        private void OnDisable()
        {
            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
                scaleRoutine = null;
            }

            if (resolvedScaleTarget != null)
                resolvedScaleTarget.localScale = normalScale;

            ApplyTint(normalColor);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AnimateTo(normalScale * hoverScale, hoverColor);

            if (GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.UiHover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateTo(normalScale, normalColor);
        }

        private void AnimateTo(Vector3 targetScale, Color targetColor)
        {
            if (scaleRoutine != null)
                StopCoroutine(scaleRoutine);

            scaleRoutine = StartCoroutine(ScaleRoutine(targetScale, targetColor));
        }

        private IEnumerator ScaleRoutine(Vector3 targetScale, Color targetColor)
        {
            Transform target = resolvedScaleTarget != null ? resolvedScaleTarget : transform;
            Vector3 from = target.localScale;
            CaptureTintFromColors();
            float elapsed = 0f;
            float animDuration = Mathf.Max(0.01f, duration);

            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animDuration);
                // Smoothstep for a softer feel
                t = t * t * (3f - 2f * t);
                target.localScale = Vector3.LerpUnclamped(from, targetScale, t);
                ApplyTintLerp(t, targetColor);
                yield return null;
            }

            target.localScale = targetScale;
            ApplyTint(targetColor);
            scaleRoutine = null;
        }

        private void ResolveTintTargets()
        {
            resolvedTintTargets.Clear();

            if (!tintOnHover)
                return;

            if (tintTargets != null && tintTargets.Length > 0)
            {
                for (int i = 0; i < tintTargets.Length; i++)
                {
                    if (tintTargets[i] != null)
                        resolvedTintTargets.Add(tintTargets[i]);
                }

                return;
            }

            Transform searchRoot = resolvedScaleTarget != null ? resolvedScaleTarget : transform;
            Graphic[] graphics = searchRoot.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                // Skip fully transparent hit areas
                if (graphic == null || graphic.color.a <= 0.001f)
                    continue;

                resolvedTintTargets.Add(graphic);
            }
        }

        private void CaptureTintFromColors()
        {
            tintFromColors.Clear();
            for (int i = 0; i < resolvedTintTargets.Count; i++)
            {
                Graphic graphic = resolvedTintTargets[i];
                tintFromColors.Add(graphic != null ? graphic.color : normalColor);
            }
        }

        private void ApplyTint(Color color)
        {
            if (!tintOnHover)
                return;

            for (int i = 0; i < resolvedTintTargets.Count; i++)
            {
                Graphic graphic = resolvedTintTargets[i];
                if (graphic != null)
                    graphic.color = color;
            }
        }

        private void ApplyTintLerp(float t, Color targetColor)
        {
            if (!tintOnHover)
                return;

            for (int i = 0; i < resolvedTintTargets.Count; i++)
            {
                Graphic graphic = resolvedTintTargets[i];
                if (graphic == null)
                    continue;

                Color from = i < tintFromColors.Count ? tintFromColors[i] : normalColor;
                graphic.color = Color.LerpUnclamped(from, targetColor, t);
            }
        }
    }
}

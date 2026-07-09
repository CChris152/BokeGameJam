using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 鼠标悬停时轻微放大 UI 元素，移开后恢复。
    /// </summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("悬停时的目标缩放倍数")]
        [SerializeField] private float hoverScale = 1.08f;

        [Tooltip("放大/缩小动画时长（秒）")]
        [SerializeField] private float duration = 0.12f;

        private Vector3 normalScale;
        private Coroutine scaleRoutine;

        private void Awake()
        {
            normalScale = transform.localScale;
        }

        private void OnDisable()
        {
            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
                scaleRoutine = null;
            }

            transform.localScale = normalScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AnimateTo(normalScale * hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateTo(normalScale);
        }

        private void AnimateTo(Vector3 targetScale)
        {
            if (scaleRoutine != null)
                StopCoroutine(scaleRoutine);

            scaleRoutine = StartCoroutine(ScaleRoutine(targetScale));
        }

        private IEnumerator ScaleRoutine(Vector3 targetScale)
        {
            Vector3 from = transform.localScale;
            float elapsed = 0f;
            float animDuration = Mathf.Max(0.01f, duration);

            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animDuration);
                // Smoothstep for a softer feel
                t = t * t * (3f - 2f * t);
                transform.localScale = Vector3.LerpUnclamped(from, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleRoutine = null;
        }
    }
}

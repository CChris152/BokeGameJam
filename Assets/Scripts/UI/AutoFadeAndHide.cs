using System.Collections;
using UnityEngine;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 等待一段时间后淡出，再隐藏自身（用于 Teach 等一次性提示）。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class AutoFadeAndHide : MonoBehaviour
    {
        [SerializeField] private float visibleDuration = 5f;
        [SerializeField] private float fadeDuration = 0.75f;
        [SerializeField] private bool destroyAfterHide;

        private CanvasGroup canvasGroup;
        private Coroutine running;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 1f;
        }

        private void OnEnable()
        {
            if (running != null)
                StopCoroutine(running);
            running = StartCoroutine(HideRoutine());
        }

        private void OnDisable()
        {
            if (running == null)
                return;

            StopCoroutine(running);
            running = null;
        }

        private IEnumerator HideRoutine()
        {
            if (visibleDuration > 0f)
                yield return new WaitForSeconds(visibleDuration);

            float duration = Mathf.Max(0f, fadeDuration);
            if (duration <= 0f)
            {
                canvasGroup.alpha = 0f;
            }
            else
            {
                float start = canvasGroup.alpha;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    canvasGroup.alpha = Mathf.Lerp(start, 0f, t);
                    yield return null;
                }

                canvasGroup.alpha = 0f;
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            running = null;

            if (destroyAfterHide)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}

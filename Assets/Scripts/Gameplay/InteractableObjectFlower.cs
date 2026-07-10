using System.Collections;
using UnityEngine;

namespace BokeGameJam.Gameplay
{
    public enum FlowerColor
    {
        Red = 0,
        Yellow = 1,
        Purple = 2,
        Blue = 3
    }

    /// <summary>
    /// 可交互物体 A 变体：花。
    /// 与普通 A 一样可捡起 / 丢弃 / 交付；摘取后原位置 5 秒刷新。
    /// 持有副本被丢弃后会快速淡出并销毁（交付消耗仍走 PlayerInteractor.ConsumeHeldItem）。
    /// 关卡一通常共五朵（多种植会打 Warning）。
    /// </summary>
    public class InteractableObjectFlower : InteractableObject
    {
        private static int plantedCount;

        [Header("Flower")]
        [SerializeField] private FlowerColor flowerColor = FlowerColor.Red;
        [Tooltip("摘取后多少秒在原位置重新可摘。")]
        [SerializeField] private float respawnDelaySeconds = 5f;
        [Tooltip("丢弃后淡出销毁时长（秒）。")]
        [SerializeField] private float discardFadeDuration = 0.25f;

        private bool isHeldCopy;
        private bool isDepleted;
        private bool isDiscarding;
        private Coroutine respawnRoutine;
        private Coroutine discardFadeRoutine;

        public FlowerColor ColorKind => flowerColor;
        public bool IsDepleted => isDepleted;
        public bool IsHeldCopy => isHeldCopy;

        private void Start()
        {
            if (isHeldCopy)
                return;

            plantedCount++;
            if (plantedCount > 5)
            {
                Debug.LogWarning(
                    $"[InteractableObjectFlower] 本关已种植 {plantedCount} 朵花（'{name}'），关卡一建议共五朵。",
                    this);
            }
        }

        private void OnEnable()
        {
            // 层级切换会停掉协程；仍 depleted 时重新开始刷新计时。
            if (!isHeldCopy && isDepleted && respawnRoutine == null && isActiveAndEnabled)
                respawnRoutine = StartCoroutine(RespawnAfterDelay());

            // 丢弃淡出中被禁用后恢复时，继续淡出销毁。
            if (isHeldCopy && isDiscarding && discardFadeRoutine == null && isActiveAndEnabled)
                discardFadeRoutine = StartCoroutine(DiscardFadeAndDestroy());
        }

        private void OnDisable()
        {
            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }

            if (discardFadeRoutine != null)
            {
                StopCoroutine(discardFadeRoutine);
                discardFadeRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (!isHeldCopy && plantedCount > 0)
                plantedCount--;
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (!base.CanInteract(interactor))
                return false;

            if (isDiscarding)
                return false;

            if (isHeldCopy)
                return !IsHeld;

            return !isDepleted && !IsHeld;
        }

        public override bool Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return false;

            // 已摘下的花：与普通 A 一样再次捡起，不再触发刷新。
            if (isHeldCopy)
                return interactor != null && interactor.TryPickUp(this);

            if (interactor == null)
                return false;

            InteractableObjectFlower cut = Instantiate(this, transform.position, transform.rotation);
            cut.PrepareAsHeldCopy();
            cut.name = name + "_Held";

            BeginDepleted();

            if (!interactor.TryPickUp(cut))
            {
                Destroy(cut.gameObject);
                CancelDepleted();
                return false;
            }

            return true;
        }

        /// <summary>丢弃持有副本：落地后快速淡出并销毁。</summary>
        public override void Drop(Vector2 worldPosition)
        {
            base.Drop(worldPosition);

            if (!isHeldCopy || isDiscarding)
                return;

            BeginDiscardFade();
        }

        private void PrepareAsHeldCopy()
        {
            isHeldCopy = true;
            isDepleted = false;
            isDiscarding = false;

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }

            if (discardFadeRoutine != null)
            {
                StopCoroutine(discardFadeRoutine);
                discardFadeRoutine = null;
            }
        }

        private void BeginDiscardFade()
        {
            isDiscarding = true;

            if (Col != null)
                Col.enabled = false;

            if (discardFadeRoutine != null)
                StopCoroutine(discardFadeRoutine);

            discardFadeRoutine = StartCoroutine(DiscardFadeAndDestroy());
        }

        private IEnumerator DiscardFadeAndDestroy()
        {
            SpriteRenderer sr = SpriteRenderer;
            Color start = sr != null ? sr.color : Color.white;
            float duration = Mathf.Max(0.01f, discardFadeDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (sr != null)
                {
                    Color c = start;
                    c.a = Mathf.Lerp(start.a, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            if (sr != null)
            {
                Color c = start;
                c.a = 0f;
                sr.color = c;
            }

            discardFadeRoutine = null;
            Destroy(gameObject);
        }

        private void BeginDepleted()
        {
            isDepleted = true;

            if (Col != null)
                Col.enabled = false;

            if (SpriteRenderer != null)
                SpriteRenderer.enabled = false;

            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);

            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private void CancelDepleted()
        {
            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }

            isDepleted = false;

            if (Col != null)
                Col.enabled = true;

            if (SpriteRenderer != null)
                SpriteRenderer.enabled = true;
        }

        private IEnumerator RespawnAfterDelay()
        {
            float delay = Mathf.Max(0f, respawnDelaySeconds);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            isDepleted = false;

            if (Col != null)
                Col.enabled = true;

            if (SpriteRenderer != null)
                SpriteRenderer.enabled = true;

            respawnRoutine = null;
        }
    }
}

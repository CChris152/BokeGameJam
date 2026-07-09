using System.Collections;
using UnityEngine;

namespace BokeGameJam.Gameplay
{
    public enum FlowerColor
    {
        Red = 0,
        Yellow = 1
    }

    /// <summary>
    /// 可交互物体 A 变体：花。
    /// 与普通 A 一样可捡起 / 丢弃 / 交付；摘取后原位置 5 秒刷新。
    /// 关卡一通常共五朵（多种植会打 Warning）。
    /// </summary>
    public class InteractableObjectFlower : InteractableObject
    {
        private static int plantedCount;

        [Header("Flower")]
        [SerializeField] private FlowerColor flowerColor = FlowerColor.Red;
        [Tooltip("摘取后多少秒在原位置重新可摘。")]
        [SerializeField] private float respawnDelaySeconds = 5f;

        private bool isHeldCopy;
        private bool isDepleted;
        private Coroutine respawnRoutine;

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
        }

        private void OnDisable()
        {
            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (!isHeldCopy && plantedCount > 0)
                plantedCount--;
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
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

        private void PrepareAsHeldCopy()
        {
            isHeldCopy = true;
            isDepleted = false;

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }
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

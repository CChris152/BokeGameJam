using System.Collections;
using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 壁灯组：默认全暗；交互后按编号顺序闪烁（默认 3→4→1→2→6→5），
    /// 闪烁结束后等待一段时间再恢复可交互。六个灯做在同一预制体里。
    /// </summary>
    public class InteractableObjectWallLampGroup : InteractableObject
    {
        private const string OffSpriteResourcePath = "Art/Pictures/关灯壁灯";
        private const string OnSpriteResourcePath = "Art/Pictures/开灯壁灯";

        [Header("Wall Lamps")]
        [Tooltip("按编号 1..N 顺序填入；下标 0 = 1 号灯。")]
        [SerializeField] private SpriteRenderer[] lamps;
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Sprite onSprite;

        [Header("Flash Sequence")]
        [Tooltip("1-based 灯编号闪烁顺序。")]
        [SerializeField] private int[] flashOrder = { 3, 4, 1, 2, 6, 5 };
        [SerializeField] private float flashOnSeconds = 0.4f;
        [SerializeField] private float flashGapSeconds = 0.15f;
        [Tooltip("整段闪烁结束后，多久恢复可交互。")]
        [SerializeField] private float cooldownAfterSequenceSeconds = 2f;

        private bool busy;
        private Coroutine sequenceRoutine;

        public override InteractMode Mode => InteractMode.Trigger;
        public bool IsBusy => busy;

        protected override void Awake()
        {
            base.Awake();
            EnsureSprites();
            ApplyAllOff();
        }

        private void OnDisable()
        {
            if (sequenceRoutine != null)
            {
                StopCoroutine(sequenceRoutine);
                sequenceRoutine = null;
            }

            busy = false;
            ApplyAllOff();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            return !busy && isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            if (sequenceRoutine != null)
                StopCoroutine(sequenceRoutine);

            sequenceRoutine = StartCoroutine(PlayFlashSequence());
        }

        private IEnumerator PlayFlashSequence()
        {
            busy = true;
            EnsureSprites();

            if (flashOrder != null)
            {
                for (int i = 0; i < flashOrder.Length; i++)
                {
                    int lampNumber = flashOrder[i];
                    SetLampLit(lampNumber, true);

                    float onDuration = Mathf.Max(0.01f, flashOnSeconds);
                    yield return new WaitForSeconds(onDuration);

                    SetLampLit(lampNumber, false);

                    if (i < flashOrder.Length - 1)
                    {
                        float gap = Mathf.Max(0f, flashGapSeconds);
                        if (gap > 0f)
                            yield return new WaitForSeconds(gap);
                    }
                }
            }

            ApplyAllOff();

            float cooldown = Mathf.Max(0f, cooldownAfterSequenceSeconds);
            if (cooldown > 0f)
                yield return new WaitForSeconds(cooldown);

            busy = false;
            sequenceRoutine = null;
        }

        private void SetLampLit(int lampNumber1Based, bool lit)
        {
            SpriteRenderer sr = GetLamp(lampNumber1Based);
            if (sr == null)
                return;

            Sprite sprite = lit ? onSprite : offSprite;
            if (sprite != null)
                sr.sprite = sprite;
        }

        private SpriteRenderer GetLamp(int lampNumber1Based)
        {
            if (lamps == null || lamps.Length == 0)
                return null;

            int index = lampNumber1Based - 1;
            if (index < 0 || index >= lamps.Length)
            {
                Debug.LogWarning(
                    $"[InteractableObjectWallLampGroup] '{name}' flash order references lamp #{lampNumber1Based}, but only {lamps.Length} lamps are assigned.",
                    this);
                return null;
            }

            return lamps[index];
        }

        private void ApplyAllOff()
        {
            EnsureSprites();
            if (lamps == null)
                return;

            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null)
                    continue;

                if (offSprite != null)
                    lamps[i].sprite = offSprite;
            }
        }

        private void EnsureSprites()
        {
            if (offSprite == null)
                offSprite = Resources.Load<Sprite>(OffSpriteResourcePath);
            if (onSprite == null)
                onSprite = Resources.Load<Sprite>(OnSpriteResourcePath);

            if (offSprite == null)
                Debug.LogError($"[InteractableObjectWallLampGroup] Missing sprite Resources/{OffSpriteResourcePath}", this);
            if (onSprite == null)
                Debug.LogError($"[InteractableObjectWallLampGroup] Missing sprite Resources/{OnSpriteResourcePath}", this);
        }
    }
}

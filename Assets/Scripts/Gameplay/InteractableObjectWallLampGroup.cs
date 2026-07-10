using System.Collections;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Data;
using BokeGameJam.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 壁灯组：默认全暗；交互后按编号顺序闪烁（默认 3→4→1→2→6→5），
    /// 闪烁结束后广播 <see cref="GameEvents.WallLampSequenceCompleted"/>（payload = 闪烁顺序），
    /// 再等待一段时间恢复可交互。六个灯做在同一预制体里。
    /// </summary>
    public class InteractableObjectWallLampGroup : InteractableObject
    {
        private const string OffSpriteResourcePath = "Art/Pictures/关灯壁灯";
        private const string OnSpriteResourcePath = "Art/Pictures/开灯壁灯";
        private const string DefaultFirstFlashStoryPath = "ScriptableObjects/Stories/Story15";

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

        [Header("First Flash Story")]
        [Tooltip("本关第一次触发闪烁时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence firstFlashStory;
        [SerializeField] private string firstFlashStoryResourcePath = DefaultFirstFlashStoryPath;
        [SerializeField] private bool playStoryOnFirstFlash = true;

        private bool busy;
        private Coroutine sequenceRoutine;
        private bool hasPlayedFirstFlashStory;

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

            TryPlayFirstFlashStory();

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

            int[] orderPayload = flashOrder != null
                ? (int[])flashOrder.Clone()
                : System.Array.Empty<int>();
            EventManager.Emit(GameEvents.WallLampSequenceCompleted, orderPayload);

            float cooldown = Mathf.Max(0f, cooldownAfterSequenceSeconds);
            if (cooldown > 0f)
                yield return new WaitForSeconds(cooldown);

            busy = false;
            sequenceRoutine = null;
        }

        private void TryPlayFirstFlashStory()
        {
            if (!playStoryOnFirstFlash || hasPlayedFirstFlashStory)
                return;

            hasPlayedFirstFlashStory = true;
            PlayBannerStory(ResolveFirstFlashStory(), "壁灯首次闪烁剧情");
        }

        private StorySequence ResolveFirstFlashStory()
        {
            if (firstFlashStory != null)
                return firstFlashStory;

            if (string.IsNullOrWhiteSpace(firstFlashStoryResourcePath))
                return null;

            return Resources.Load<StorySequence>(firstFlashStoryResourcePath.Trim());
        }

        private void PlayBannerStory(StorySequence story, string storyLabel)
        {
            if (story == null || !story.HasLines)
            {
                Debug.LogWarning($"[InteractableObjectWallLampGroup] {storyLabel}配置缺失或为空。", this);
                return;
            }

            CameraTopBannerUI banner = EnsureTopBanner();
            if (banner == null)
            {
                Debug.LogWarning(
                    $"[InteractableObjectWallLampGroup] CameraTopBannerUI 未找到，无法播放{storyLabel}。",
                    this);
                return;
            }

            banner.PlayStory(story.CreateLineList());
        }

        private static CameraTopBannerUI EnsureTopBanner()
        {
            CameraTopBannerUI banner = CameraTopBannerUI.Instance;
            if (banner != null)
                return banner;

            if (UIManager.Instance == null)
                return null;

            GameObject go = UIManager.Instance.Load(CameraTopBannerUI.ResourceId);
            return go != null ? go.GetComponent<CameraTopBannerUI>() : null;
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

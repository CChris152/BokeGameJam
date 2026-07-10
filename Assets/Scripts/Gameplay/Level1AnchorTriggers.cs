using System.Collections;
using BokeGameJam.CameraSystem;
using BokeGameJam.Core;
using BokeGameJam.Data;
using BokeGameJam.Input;
using BokeGameJam.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// Level1 分段相机：Start 固定到 Place1；
    /// 玩家 X 从左到右越过 Anchor1 → 平滑移到 Place2；
    /// 越过 Anchor2 → Place3；反向越过则反向切换。
    /// 子物体命名：Place1 / Place2 / Place3 / Anchor1 / Anchor2（也可在 Inspector 拖引用）。
    /// 开场可播放配置的剧情字幕。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class Level1AnchorTriggers : MonoBehaviour
    {
        private const string TargetSceneName = "Level1";
        private const string DefaultIntroStoryResourcePath = "ScriptableObjects/Stories/Story1";
        private const string DefaultFirstFlowerStoryResourcePath = "ScriptableObjects/Stories/Story2";
        private const string DefaultFirstShiftStoryResourcePath = "ScriptableObjects/Stories/Story3";

        [Header("Camera Places (可拖拽，留空则按子物体名查找)")]
        [SerializeField] private Transform place1;
        [SerializeField] private Transform place2;
        [SerializeField] private Transform place3;

        [Header("X Cross Anchors")]
        [SerializeField] private Transform anchor1;
        [SerializeField] private Transform anchor2;

        [Header("Move")]
        [SerializeField] private float cameraMoveDuration = 0.6f;
        [Tooltip("相机目标 Y 额外偏移（与 CameraManager followOffset.y 类似时可填 1.5）")]
        [SerializeField] private float cameraYOffset;

        [Header("Intro Story")]
        [Tooltip("开场剧情配置；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence introStory;
        [SerializeField] private string introStoryResourcePath = DefaultIntroStoryResourcePath;
        [SerializeField] private bool playIntroStoryOnStart = true;

        [Header("First Flower Story")]
        [Tooltip("本关第一次捡到任意颜色花时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence firstFlowerStory;
        [SerializeField] private string firstFlowerStoryResourcePath = DefaultFirstFlowerStoryResourcePath;
        [SerializeField] private bool playStoryOnFirstFlowerPickup = true;

        [Header("First Shift Story")]
        [Tooltip("本关第一次按 Shift（切换世界）时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence firstShiftStory;
        [SerializeField] private string firstShiftStoryResourcePath = DefaultFirstShiftStoryResourcePath;
        [SerializeField] private bool playStoryOnFirstShift = true;

        private PlayerController player;
        private PlayerInteractor playerInteractor;
        private float previousPlayerX;
        private bool hasPreviousPlayerX;
        private Coroutine moveRoutine;
        private Coroutine introStoryRoutine;
        private bool sceneReady;
        private Transform currentPlace;
        private bool hasPlayedFirstFlowerStory;
        private bool hasPlayedFirstShiftStory;

        private void Awake()
        {
            sceneReady = SceneManager.GetActiveScene().name == TargetSceneName;
            if (!sceneReady)
            {
                Debug.LogWarning(
                    $"[Level1AnchorTriggers] 当前场景不是 {TargetSceneName}，组件已禁用。",
                    this);
                enabled = false;
                return;
            }

            ResolveReferences();
        }

        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name != TargetSceneName)
                return;

            EventManager.On(InputEvents.WorldToggle, OnWorldTogglePressed);
        }

        private void Start()
        {
            if (!sceneReady)
                return;

            // 停掉跟随，避免与分段固定相机抢控制权。
            if (CameraManager.Instance != null)
                CameraManager.Instance.SetFollowTarget(null);

            SnapCameraTo(place1);
            currentPlace = place1;

            if (playIntroStoryOnStart)
                introStoryRoutine = StartCoroutine(PlayStoryNextFrame(ResolveIntroStory(), "开场剧情"));
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.WorldToggle, OnWorldTogglePressed);

            if (introStoryRoutine != null)
            {
                StopCoroutine(introStoryRoutine);
                introStoryRoutine = null;
            }
        }

        /// <summary>等一帧，确保 GameManager 已加载 CameraTopBanner 后再播剧情。</summary>
        private IEnumerator PlayStoryNextFrame(StorySequence story, string storyLabel)
        {
            yield return null;
            PlayStoryNow(story, storyLabel);
            introStoryRoutine = null;
        }

        private void PlayStoryNow(StorySequence story, string storyLabel)
        {
            if (story == null || !story.HasLines)
            {
                Debug.LogWarning($"[Level1AnchorTriggers] {storyLabel}配置缺失或为空。", this);
                return;
            }

            CameraTopBannerUI banner = CameraTopBannerUI.Instance;
            if (banner == null && UIManager.Instance != null)
                banner = UIManager.Instance.ShowTopBanner();

            if (banner == null)
            {
                Debug.LogWarning(
                    $"[Level1AnchorTriggers] CameraTopBannerUI 未找到，无法播放{storyLabel}。",
                    this);
                return;
            }

            banner.PlayStory(story.CreateLineList());
        }

        private static StorySequence ResolveStory(StorySequence assigned, string resourcePath)
        {
            if (assigned != null)
                return assigned;

            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            return Resources.Load<StorySequence>(resourcePath.Trim());
        }

        private StorySequence ResolveIntroStory()
        {
            return ResolveStory(introStory, introStoryResourcePath);
        }

        private StorySequence ResolveFirstFlowerStory()
        {
            return ResolveStory(firstFlowerStory, firstFlowerStoryResourcePath);
        }

        private StorySequence ResolveFirstShiftStory()
        {
            return ResolveStory(firstShiftStory, firstShiftStoryResourcePath);
        }

        /// <summary>本关第一次按 Shift（世界切换）时播放 Story3。</summary>
        private void OnWorldTogglePressed()
        {
            if (!sceneReady || !playStoryOnFirstShift || hasPlayedFirstShiftStory)
                return;

            hasPlayedFirstShiftStory = true;
            PlayStoryNow(ResolveFirstShiftStory(), "首次Shift剧情");
        }

        private void Update()
        {
            if (!sceneReady)
                return;

            if (!TryGetPlayer(out Transform playerTransform))
                return;

            TryTriggerFirstFlowerStory();

            float x = playerTransform.position.x;
            if (!hasPreviousPlayerX)
            {
                previousPlayerX = x;
                hasPreviousPlayerX = true;
                return;
            }

            float prevX = previousPlayerX;
            previousPlayerX = x;

            // Anchor1：左→右到 Place2；右→左到 Place1
            if (anchor1 != null)
            {
                float a1 = anchor1.position.x;
                if (prevX < a1 && x >= a1)
                    MoveCameraTo(place2);
                else if (prevX > a1 && x <= a1)
                    MoveCameraTo(place1);
            }

            // Anchor2：左→右到 Place3；右→左到 Place2
            if (anchor2 != null)
            {
                float a2 = anchor2.position.x;
                if (prevX < a2 && x >= a2)
                    MoveCameraTo(place3);
                else if (prevX > a2 && x <= a2)
                    MoveCameraTo(place2);
            }
        }

        private void ResolveReferences()
        {
            if (place1 == null)
                place1 = transform.Find("Place1");
            if (place2 == null)
                place2 = transform.Find("Place2");
            if (place3 == null)
                place3 = transform.Find("Place3");
            if (anchor1 == null)
                anchor1 = transform.Find("Anchor1");
            if (anchor2 == null)
                anchor2 = transform.Find("Anchor2");

            if (place1 == null || place2 == null || place3 == null || anchor1 == null || anchor2 == null)
            {
                Debug.LogWarning(
                    "[Level1AnchorTriggers] 缺少 Place1/2/3 或 Anchor1/2，请检查子物体或 Inspector 引用。",
                    this);
            }
        }

        private bool TryGetPlayer(out Transform playerTransform)
        {
            playerTransform = null;
            if (player == null)
                player = FindObjectOfType<PlayerController>();

            if (player == null)
                return false;

            playerTransform = player.transform;
            return true;
        }

        /// <summary>本关第一次捡到任意颜色花时播放 Story2。</summary>
        private void TryTriggerFirstFlowerStory()
        {
            if (!playStoryOnFirstFlowerPickup || hasPlayedFirstFlowerStory)
                return;

            if (!TryGetPlayerInteractor(out PlayerInteractor interactor))
                return;

            if (interactor.HeldItem is not InteractableObjectFlower)
                return;

            hasPlayedFirstFlowerStory = true;
            PlayStoryNow(ResolveFirstFlowerStory(), "首次摘花剧情");
        }

        private bool TryGetPlayerInteractor(out PlayerInteractor interactor)
        {
            interactor = playerInteractor;
            if (interactor != null)
                return true;

            if (player != null)
                interactor = player.GetComponent<PlayerInteractor>();

            if (interactor == null)
                interactor = FindObjectOfType<PlayerInteractor>();

            playerInteractor = interactor;
            return interactor != null;
        }

        private void SnapCameraTo(Transform place)
        {
            if (place == null)
                return;

            Camera cam = GetCamera();
            if (cam == null)
                return;

            Vector3 p = cam.transform.position;
            p.x = place.position.x;
            p.y = place.position.y + cameraYOffset;
            cam.transform.position = p;
            currentPlace = place;

            if (CameraManager.Instance != null)
                CameraManager.Instance.SnapTo(p);
        }

        private void MoveCameraTo(Transform place)
        {
            if (place == null || place == currentPlace)
                return;

            currentPlace = place;

            if (CameraManager.Instance != null)
                CameraManager.Instance.SetFollowTarget(null);

            if (moveRoutine != null)
                StopCoroutine(moveRoutine);

            moveRoutine = StartCoroutine(MoveCameraRoutine(place));
        }

        private IEnumerator MoveCameraRoutine(Transform place)
        {
            Camera cam = GetCamera();
            if (cam == null || place == null)
            {
                moveRoutine = null;
                yield break;
            }

            Vector3 start = cam.transform.position;
            Vector3 end = new(
                place.position.x,
                place.position.y + cameraYOffset,
                start.z);

            float duration = Mathf.Max(0.01f, cameraMoveDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = t * t * (3f - 2f * t); // smoothstep
                cam.transform.position = Vector3.Lerp(start, end, s);
                yield return null;
            }

            cam.transform.position = end;
            moveRoutine = null;
        }

        private static Camera GetCamera()
        {
            if (CameraManager.Instance != null && CameraManager.Instance.Camera != null)
                return CameraManager.Instance.Camera;
            return Camera.main;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawPlaceGizmo(place1 != null ? place1 : transform.Find("Place1"), Color.cyan);
            DrawPlaceGizmo(place2 != null ? place2 : transform.Find("Place2"), Color.green);
            DrawPlaceGizmo(place3 != null ? place3 : transform.Find("Place3"), Color.yellow);
            DrawAnchorLine(anchor1 != null ? anchor1 : transform.Find("Anchor1"), Color.magenta);
            DrawAnchorLine(anchor2 != null ? anchor2 : transform.Find("Anchor2"), Color.red);
        }

        private static void DrawPlaceGizmo(Transform t, Color color)
        {
            if (t == null)
                return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(t.position, 0.4f);
        }

        private static void DrawAnchorLine(Transform t, Color color)
        {
            if (t == null)
                return;
            Gizmos.color = color;
            Vector3 p = t.position;
            Gizmos.DrawLine(p + Vector3.up * 5f, p + Vector3.down * 5f);
        }
#endif
    }
}

using System.Collections;
using System.Collections.Generic;
using BokeGameJam.CameraSystem;
using BokeGameJam.Data;
using BokeGameJam.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 通用分段相机：Start 固定到第一个 Place；
    /// 玩家 X 越过 Anchor[i] 时在 Place[i] / Place[i+1] 间切换。
    /// 子物体可命名 Place1..N / Anchor1..N-1（也可在 Inspector 拖引用）。
    /// 可选开场剧情（如 Level2）。第一关请使用 <see cref="Level1AnchorTriggers"/>。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class LevelAnchorTriggers : MonoBehaviour
    {
        private const string DefaultIntroStoryResourcePath = "ScriptableObjects/Stories/Story10";
        private const string DefaultPlace3StoryResourcePath = "ScriptableObjects/Stories/Story11";
        private const string DefaultPlace5StoryResourcePath = "ScriptableObjects/Stories/Story14";

        /// <summary>第三个 Place（0-based index=2）首次到达时触发剧情。</summary>
        private const int Place3Index = 2;
        /// <summary>第五个 Place（0-based index=4）首次到达时触发剧情。</summary>
        private const int Place5Index = 4;

        [Header("Scene")]
        [Tooltip("留空则任意场景可用；填写后仅在该场景名下启用。")]
        [SerializeField] private string targetSceneName;

        [Header("Camera Places (从左到右)")]
        [SerializeField] private Transform[] places;

        [Header("X Cross Anchors (从左到右，数量应为 Places-1)")]
        [SerializeField] private Transform[] anchors;

        [Header("Move")]
        [SerializeField] private float cameraMoveDuration = 0.6f;
        [Tooltip("相机目标 Y 额外偏移（与 CameraManager followOffset.y 类似时可填 1.5）")]
        [SerializeField] private float cameraYOffset;

        [Header("Intro Story")]
        [Tooltip("开场剧情配置；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence introStory;
        [SerializeField] private string introStoryResourcePath = DefaultIntroStoryResourcePath;
        [SerializeField] private bool playIntroStoryOnStart = true;

        [Header("Place3 Story")]
        [Tooltip("首次镜头切到 Place3 时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence place3Story;
        [SerializeField] private string place3StoryResourcePath = DefaultPlace3StoryResourcePath;
        [SerializeField] private bool playStoryOnFirstReachPlace3 = true;

        [Header("Place5 Story")]
        [Tooltip("首次镜头切到 Place5 时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence place5Story;
        [SerializeField] private string place5StoryResourcePath = DefaultPlace5StoryResourcePath;
        [SerializeField] private bool playStoryOnFirstReachPlace5 = true;

        private PlayerController player;
        private float previousPlayerX;
        private bool hasPreviousPlayerX;
        private Coroutine moveRoutine;
        private Coroutine introStoryRoutine;
        private bool sceneReady;
        private Transform currentPlace;
        private bool hasPlayedPlace3Story;
        private bool hasPlayedPlace5Story;

        private void Awake()
        {
            sceneReady = string.IsNullOrEmpty(targetSceneName)
                || SceneManager.GetActiveScene().name == targetSceneName;
            if (!sceneReady)
            {
                Debug.LogWarning(
                    $"[LevelAnchorTriggers] 当前场景不是 {targetSceneName}，组件已禁用。",
                    this);
                enabled = false;
                return;
            }

            ResolveReferences();
            if (!ValidateLayout())
            {
                enabled = false;
                sceneReady = false;
            }
        }

        private void OnDisable()
        {
            if (introStoryRoutine != null)
            {
                StopCoroutine(introStoryRoutine);
                introStoryRoutine = null;
            }
        }

        private void Start()
        {
            if (!sceneReady || places == null || places.Length == 0)
                return;

            if (CameraManager.Instance != null)
                CameraManager.Instance.SetFollowTarget(null);

            Transform startPlace = places[0];
            SnapCameraTo(startPlace);
            currentPlace = startPlace;

            // 等一帧：先让 GameManager 走 LevelPlaying 加载；若仍没有则自行补加载。
            introStoryRoutine = StartCoroutine(EnsureBannerThenPlayIntro());
        }

        /// <summary>
        /// 进入关卡后检查 CameraTopBanner；缺失时按 Level1 方式经 UIManager 加载。
        /// </summary>
        private IEnumerator EnsureBannerThenPlayIntro()
        {
            yield return null;

            if (EnsureTopBanner() == null)
            {
                Debug.LogWarning(
                    "[LevelAnchorTriggers] CameraTopBannerUI 未找到且无法加载。",
                    this);
            }

            if (playIntroStoryOnStart)
                PlayStoryNow(ResolveIntroStory(), "开场剧情");

            introStoryRoutine = null;
        }

        /// <summary>
        /// 与 Level1 / GameManager 一致：已有 Instance 则复用，否则 UIManager.Load("CameraTopBanner")。
        /// </summary>
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

        private void PlayStoryNow(StorySequence story, string storyLabel)
        {
            if (story == null || !story.HasLines)
            {
                Debug.LogWarning($"[LevelAnchorTriggers] {storyLabel}配置缺失或为空。", this);
                return;
            }

            CameraTopBannerUI banner = EnsureTopBanner();
            if (banner == null)
            {
                Debug.LogWarning(
                    $"[LevelAnchorTriggers] CameraTopBannerUI 未找到，无法播放{storyLabel}。",
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

        private StorySequence ResolvePlace3Story()
        {
            return ResolveStory(place3Story, place3StoryResourcePath);
        }

        private StorySequence ResolvePlace5Story()
        {
            return ResolveStory(place5Story, place5StoryResourcePath);
        }

        private void Update()
        {
            if (!sceneReady)
                return;

            if (!TryGetPlayer(out Transform playerTransform))
                return;

            float x = playerTransform.position.x;
            if (!hasPreviousPlayerX)
            {
                previousPlayerX = x;
                hasPreviousPlayerX = true;
                return;
            }

            float prevX = previousPlayerX;
            previousPlayerX = x;

            for (int i = 0; i < anchors.Length; i++)
            {
                Transform anchor = anchors[i];
                if (anchor == null)
                    continue;

                float ax = anchor.position.x;
                if (prevX < ax && x >= ax)
                {
                    int placeIndex = i + 1;
                    MoveCameraTo(places[placeIndex]);
                    if (placeIndex == Place3Index)
                        TryPlayPlace3Story();
                    else if (placeIndex == Place5Index)
                        TryPlayPlace5Story();
                }
                else if (prevX > ax && x <= ax)
                {
                    MoveCameraTo(places[i]);
                }
            }
        }

        /// <summary>首次镜头切到 Place3 时播放剧情（仅一次）。</summary>
        private void TryPlayPlace3Story()
        {
            if (!playStoryOnFirstReachPlace3 || hasPlayedPlace3Story)
                return;

            hasPlayedPlace3Story = true;
            PlayStoryNow(ResolvePlace3Story(), "Place3剧情");
        }

        /// <summary>首次镜头切到 Place5 时播放剧情（仅一次）。</summary>
        private void TryPlayPlace5Story()
        {
            if (!playStoryOnFirstReachPlace5 || hasPlayedPlace5Story)
                return;

            hasPlayedPlace5Story = true;
            PlayStoryNow(ResolvePlace5Story(), "Place5剧情");
        }

        private void ResolveReferences()
        {
            if (places == null || places.Length == 0)
                places = FindNumberedChildren("Place");
            if (anchors == null || anchors.Length == 0)
                anchors = FindNumberedChildren("Anchor");
        }

        private Transform[] FindNumberedChildren(string prefix)
        {
            var list = new List<Transform>();
            for (int i = 1; i <= 32; i++)
            {
                Transform child = transform.Find(prefix + i);
                if (child == null)
                    break;
                list.Add(child);
            }

            return list.ToArray();
        }

        private bool ValidateLayout()
        {
            if (places == null || places.Length == 0)
            {
                Debug.LogWarning("[LevelAnchorTriggers] places 为空，请配置 Place 或子物体 Place1..N。", this);
                return false;
            }

            if (anchors == null)
                anchors = System.Array.Empty<Transform>();

            if (places.Length != anchors.Length + 1)
            {
                Debug.LogWarning(
                    $"[LevelAnchorTriggers] 数量不匹配：places={places.Length}，anchors={anchors.Length}，应为 places == anchors + 1。",
                    this);
                return false;
            }

            for (int i = 0; i < places.Length; i++)
            {
                if (places[i] == null)
                {
                    Debug.LogWarning($"[LevelAnchorTriggers] places[{i}] 为空。", this);
                    return false;
                }
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null)
                {
                    Debug.LogWarning($"[LevelAnchorTriggers] anchors[{i}] 为空。", this);
                    return false;
                }
            }

            return true;
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
                float s = t * t * (3f - 2f * t);
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
            Transform[] drawPlaces = places != null && places.Length > 0
                ? places
                : FindNumberedChildren("Place");
            Transform[] drawAnchors = anchors != null && anchors.Length > 0
                ? anchors
                : FindNumberedChildren("Anchor");

            for (int i = 0; i < drawPlaces.Length; i++)
            {
                float t = drawPlaces.Length <= 1 ? 0f : i / (float)(drawPlaces.Length - 1);
                DrawPlaceGizmo(drawPlaces[i], Color.Lerp(Color.cyan, Color.yellow, t));
            }

            for (int i = 0; i < drawAnchors.Length; i++)
            {
                float t = drawAnchors.Length <= 1 ? 0f : i / (float)(drawAnchors.Length - 1);
                DrawAnchorLine(drawAnchors[i], Color.Lerp(Color.magenta, Color.red, t));
            }
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

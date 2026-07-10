using System.Collections;
using BokeGameJam.Core;
using BokeGameJam.Levels;
using BokeGameJam.Puzzles;
using BokeGameJam.Puzzles.Mirror;
using BokeGameJam.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// Runtime composition root for the Level 3 prototype. Gameplay state is
    /// independent from presentation; Resources prefabs can replace every
    /// generated placeholder without changing the puzzle rules.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class Level3PuzzleController : MonoBehaviour
    {
        private const string TargetSceneName = "Level3";

        [Header("Password")]
        [SerializeField] private string passwordMechanismId = "level3_clock_cabinet";

        [Header("Prototype Layout")]
        [SerializeField] private Vector3 framePosition = new(0f, 1f, 0f);
        [SerializeField] private Vector3 yellowShardPosition = new(-2.5f, -3.9f, 0f);
        [SerializeField] private Vector3 redShardPosition = new(3f, 1f, 0f);
        [SerializeField] private Vector3 greenShardPosition = new(-2f, 6f, 0f);
        [SerializeField] private Vector3 lowerCodeHintPosition = new(-1.5f, -3.25f, 0f);
        [SerializeField] private Vector3 upperCodeHintPosition = new(1.5f, 6.35f, 0f);

        [Header("Future Art Resource Paths")]
        [SerializeField] private string framePrefabResourcePath = "Prefabs/Gameplay/Level3/MirrorFrame";
        [SerializeField] private string yellowShardPrefabResourcePath = "Prefabs/Gameplay/Level3/YellowMirrorShard";
        [SerializeField] private string redShardPrefabResourcePath = "Prefabs/Gameplay/Level3/RedMirrorShard";
        [SerializeField] private string greenShardPrefabResourcePath = "Prefabs/Gameplay/Level3/GreenMirrorShard";
        [SerializeField] private string lowerHintPrefabResourcePath = "Prefabs/Gameplay/Level3/LowerCodeHint";
        [SerializeField] private string upperHintPrefabResourcePath = "Prefabs/Gameplay/Level3/UpperCodeHint";

        [Header("Master Mirror Puzzle Compatibility")]
        [Tooltip("When assigned, Level 3 uses the master drag-and-snap puzzle after shard delivery.")]
        [SerializeField] private MirrorPuzzleFrame mirrorPuzzleFrameOverride;
        [SerializeField] private bool preferMasterMirrorPuzzle = true;

        [Header("Audio Hooks")]
        [SerializeField] private string shardAcceptedSfxId;
        [SerializeField] private string shardRejectedSfxId;
        [SerializeField] private string mirrorPieceSfxId;
        [SerializeField] private string puzzleCompletedSfxId;
        [SerializeField] private AudioClip shardAcceptedSfxOverride;
        [SerializeField] private AudioClip shardRejectedSfxOverride;
        [SerializeField] private AudioClip mirrorPieceSfxOverride;
        [SerializeField] private AudioClip puzzleCompletedSfxOverride;
        [SerializeField] private AudioSource localAudioSource;

        private readonly Level3PuzzleStateMachine state = new();
        private readonly Level3MirrorShard[] shards = new Level3MirrorShard[3];
        private Transform runtimeRoot;
        private PasswordLockInteractable passwordLock;
        private Level3MirrorFrame mirrorFrame;
        private MirrorPuzzleFrame activeMasterMirrorFrame;
        private bool initialized;
        private bool completionEmitted;

        public Level3PuzzleStage Stage => state.Stage;
        public bool CanCollectShards => state.CanCollectShards;
        public bool IsMirrorAssemblyReady => state.Stage == Level3PuzzleStage.MirrorAssembly;
        public MirrorShardColor? ExpectedShardColor => state.ExpectedColor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureLevel3Controller()
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    TargetSceneName,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            if (FindObjectOfType<Level3PuzzleController>(true) != null)
                return;

            new GameObject(nameof(Level3PuzzleController))
                .AddComponent<Level3PuzzleController>();
        }

        private void OnEnable()
        {
            EventManager.On<PasswordLockInteractable>(
                PasswordLockEvents.Unlocked,
                OnPasswordUnlocked);
            EventManager.On<PuzzleStateChange>(
                PuzzleStateHub.StateChanged,
                OnPuzzleStateChanged);
            EventManager.On<PuzzleSignal>(
                PuzzleSignalHub.SignalEmitted,
                OnPuzzleSignal);
        }

        private void OnDisable()
        {
            EventManager.Off<PasswordLockInteractable>(
                PasswordLockEvents.Unlocked,
                OnPasswordUnlocked);
            EventManager.Off<PuzzleStateChange>(
                PuzzleStateHub.StateChanged,
                OnPuzzleStateChanged);
            EventManager.Off<PuzzleSignal>(
                PuzzleSignalHub.SignalEmitted,
                OnPuzzleSignal);
        }

        private IEnumerator Start()
        {
            GameManager.EnsureExists();

            // LevelEditor loads JSON in Start. Wait until all generated tiles exist.
            yield return null;
            yield return null;

            if (!IsTargetScene())
            {
                enabled = false;
                yield break;
            }

            SetupRuntimePuzzle();
        }

        public void DeliverShard(PlayerInteractor interactor, Level3MirrorShard shard)
        {
            if (interactor == null || shard == null || interactor.HeldItem != shard)
                return;

            MirrorShardDeliveryResult result = state.Deliver(shard.ColorKind);
            if (result == MirrorShardDeliveryResult.Unavailable)
                return;

            InteractableObject released = interactor.ReleaseHeldItem();
            if (released != shard)
            {
                Debug.LogError("[Level3] Held shard changed during delivery.", this);
                return;
            }

            if (result == MirrorShardDeliveryResult.WrongOrder)
            {
                RestoreAllShards();
                mirrorFrame.RefreshTargetVisual();
                PlayFeedback(shardRejectedSfxOverride, shardRejectedSfxId);
                EventManager.Emit(Level3PuzzleEvents.SequenceReset);
                Debug.Log("[Level3] Mirror shard order was incorrect; sequence reset.", this);
                return;
            }

            shard.Drop(shard.Origin);
            shard.MarkDelivered();
            mirrorFrame.RefreshTargetVisual();
            PlayFeedback(shardAcceptedSfxOverride, shardAcceptedSfxId);

            if (result != MirrorShardDeliveryResult.SequenceCompleted)
                return;

            mirrorFrame.MarkSequenceComplete();
            SetAllShardsAvailable(false);
            EventManager.Emit(Level3PuzzleEvents.StageChanged, state.Stage);
            OpenMirrorAssembly();
        }

        public void OpenMirrorAssembly()
        {
            if (!IsMirrorAssemblyReady)
                return;

            if (TryOpenMasterMirrorPuzzle())
                return;

            Level3MirrorPuzzlePanel.Show(this);
        }

        public void NotifyMirrorPiecePlaced()
        {
            PlayFeedback(mirrorPieceSfxOverride, mirrorPieceSfxId);
        }

        public void CompleteMirrorAssembly()
        {
            if (!state.CompleteMirrorAssembly() || completionEmitted)
                return;

            completionEmitted = true;
            PlayFeedback(puzzleCompletedSfxOverride, puzzleCompletedSfxId);
            EventManager.Emit(Level3PuzzleEvents.StageChanged, state.Stage);

            LevelManager levelManager = LevelManager.Instance;
            if (levelManager != null && levelManager.HasCurrentLevel)
                levelManager.CompleteCurrentLevel();
            else
                EventManager.Emit(GameEvents.LevelCompleted, "level_3");

            Debug.Log("[Level3] Password, shard sequence, and mirror assembly completed.", this);
        }

        private bool TryOpenMasterMirrorPuzzle()
        {
            if (!preferMasterMirrorPuzzle)
                return false;

            MirrorPuzzleFrame frame = ResolveMasterMirrorFrame();
            if (frame == null
                || !frame.HasPanelPrefab
                || !frame.enabled
                || !frame.gameObject.activeInHierarchy
                || !frame.CanInteract(null))
            {
                return false;
            }

            activeMasterMirrorFrame = frame;
            frame.Open();
            return frame.IsOpen;
        }

        private MirrorPuzzleFrame ResolveMasterMirrorFrame()
        {
            if (mirrorPuzzleFrameOverride != null)
                return mirrorPuzzleFrameOverride;

            MirrorPuzzleFrame[] frames = FindObjectsOfType<MirrorPuzzleFrame>(true);
            for (int i = 0; i < frames.Length; i++)
            {
                MirrorPuzzleFrame candidate = frames[i];
                if (candidate != null && candidate.HasPanelPrefab)
                    return candidate;
            }

            return null;
        }

        private void OnPuzzleStateChanged(PuzzleStateChange change)
        {
            if (!IsMirrorAssemblyReady || !change.Value || change.Source == null)
                return;

            if (IsExpectedMasterFrame(change.Source))
                CompleteMirrorAssembly();
        }

        private void OnPuzzleSignal(PuzzleSignal signal)
        {
            if (!IsMirrorAssemblyReady || signal.Source == null)
                return;

            if (IsExpectedMasterFrame(signal.Source))
                CompleteMirrorAssembly();
        }

        private bool IsExpectedMasterFrame(GameObject source)
        {
            MirrorPuzzleFrame sourceFrame = source.GetComponent<MirrorPuzzleFrame>();
            if (sourceFrame == null)
                return false;

            MirrorPuzzleFrame expectedFrame =
                activeMasterMirrorFrame != null
                    ? activeMasterMirrorFrame
                    : ResolveMasterMirrorFrame();
            return expectedFrame != null && sourceFrame == expectedFrame;
        }

        private void SetupRuntimePuzzle()
        {
            if (initialized)
                return;

            initialized = true;
            runtimeRoot = new GameObject("_Level3PuzzleRuntime").transform;
            EnsureSafePlayerSpawn();

            passwordLock = FindPasswordLock();
            if (passwordLock == null)
            {
                Debug.LogError(
                    $"[Level3] Password lock '{passwordMechanismId}' was not spawned from Level3.json.",
                    this);
            }

            mirrorFrame = CreateMirrorFrame();
            shards[(int)MirrorShardColor.Yellow] = CreateShard(
                MirrorShardColor.Yellow,
                yellowShardPosition,
                yellowShardPrefabResourcePath);
            shards[(int)MirrorShardColor.Red] = CreateShard(
                MirrorShardColor.Red,
                redShardPosition,
                redShardPrefabResourcePath);
            shards[(int)MirrorShardColor.Green] = CreateShard(
                MirrorShardColor.Green,
                greenShardPosition,
                greenShardPrefabResourcePath);

            CreateWorldHint(
                "Level3CodeHint_Lower",
                "1 3 5",
                lowerCodeHintPosition,
                lowerHintPrefabResourcePath);
            CreateWorldHint(
                "Level3CodeHint_Upper",
                "6 8 9",
                upperCodeHintPosition,
                upperHintPrefabResourcePath);

            mirrorFrame.SetAvailable(false);
            SetAllShardsAvailable(false);

            if (passwordLock != null && passwordLock.IsUnlocked)
                OnPasswordUnlocked(passwordLock);
        }

        private PasswordLockInteractable FindPasswordLock()
        {
            PasswordLockInteractable[] locks =
                FindObjectsOfType<PasswordLockInteractable>(true);
            for (int i = 0; i < locks.Length; i++)
            {
                if (string.Equals(
                        locks[i].MechanismId,
                        passwordMechanismId,
                        System.StringComparison.Ordinal))
                {
                    return locks[i];
                }
            }

            return locks.Length > 0 ? locks[0] : null;
        }

        private void OnPasswordUnlocked(PasswordLockInteractable unlockedLock)
        {
            if (!initialized || unlockedLock == null || !IsTargetScene())
                return;

            if (passwordLock != null && unlockedLock != passwordLock)
                return;

            if (!state.Unlock())
                return;

            passwordLock = unlockedLock;
            passwordLock.gameObject.SetActive(false);
            mirrorFrame.SetAvailable(true);
            SetAllShardsAvailable(true);
            mirrorFrame.RefreshTargetVisual();

            EventManager.Emit(Level3PuzzleEvents.StageChanged, state.Stage);
            Debug.Log("[Level3] Cabinet unlocked; mirror shards are now interactive.", this);
        }

        private Level3MirrorFrame CreateMirrorFrame()
        {
            GameObject root = InstantiateArtOrFallback(
                framePrefabResourcePath,
                "Level3MirrorFrame",
                framePosition,
                new Vector2(1.4f, 1.8f));

            EnsureTriggerCollider(root, new Vector2(1.4f, 1.8f));
            Level3MirrorFrame frame = root.GetComponent<Level3MirrorFrame>();
            if (frame == null)
                frame = root.AddComponent<Level3MirrorFrame>();

            frame.SetLevelLayer(LevelLayer.Shared);
            frame.ApplyEditorConfig("level3_mirror");
            frame.Initialize(this);
            return frame;
        }

        private Level3MirrorShard CreateShard(
            MirrorShardColor color,
            Vector3 position,
            string resourcePath)
        {
            GameObject root = InstantiateArtOrFallback(
                resourcePath,
                $"MirrorShard_{color}",
                position,
                new Vector2(0.75f, 0.9f));

            EnsureTriggerCollider(root, new Vector2(0.8f, 0.9f));
            Level3MirrorShard shard = root.GetComponent<Level3MirrorShard>();
            if (shard == null)
                shard = root.AddComponent<Level3MirrorShard>();

            shard.SetLevelLayer(LevelLayer.Shared);
            shard.ApplyEditorConfig("level3_mirror");
            shard.Initialize(this, color);
            return shard;
        }

        private void CreateWorldHint(
            string objectName,
            string clue,
            Vector3 position,
            string resourcePath)
        {
            GameObject prefab = string.IsNullOrWhiteSpace(resourcePath)
                ? null
                : Resources.Load<GameObject>(resourcePath.Trim());
            GameObject root;
            if (prefab != null)
            {
                root = Instantiate(prefab, position, Quaternion.identity, runtimeRoot);
            }
            else
            {
                root = new GameObject(objectName);
                root.transform.SetParent(runtimeRoot, false);
                root.transform.position = position;

                TextMesh label = root.AddComponent<TextMesh>();
                label.text = clue;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 72;
                label.characterSize = 0.12f;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.color = new Color(0.95f, 0.75f, 0.22f, 1f);

                MeshRenderer meshRenderer = root.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                    meshRenderer.sortingOrder = 30;
            }

            root.name = objectName;
            Level3WorldHint hint = root.GetComponent<Level3WorldHint>();
            if (hint == null)
                hint = root.AddComponent<Level3WorldHint>();
            hint.Initialize(clue);
        }

        private GameObject InstantiateArtOrFallback(
            string resourcePath,
            string objectName,
            Vector3 position,
            Vector2 fallbackSize)
        {
            GameObject prefab = string.IsNullOrWhiteSpace(resourcePath)
                ? null
                : Resources.Load<GameObject>(resourcePath.Trim());
            GameObject root;

            if (prefab != null)
            {
                root = Instantiate(prefab, position, Quaternion.identity, runtimeRoot);
            }
            else
            {
                root = new GameObject(objectName);
                root.transform.SetParent(runtimeRoot, false);
                root.transform.position = position;

                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = CreateFallbackSprite();
                renderer.size = fallbackSize;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.sortingOrder = 20;
            }

            root.name = objectName;
            return root;
        }

        private static void EnsureTriggerCollider(GameObject target, Vector2 size)
        {
            Collider2D collider = target.GetComponent<Collider2D>();
            if (collider == null)
            {
                BoxCollider2D box = target.AddComponent<BoxCollider2D>();
                box.size = size;
                collider = box;
            }

            collider.isTrigger = true;
        }

        private void RestoreAllShards()
        {
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] != null)
                    shards[i].RestoreToOrigin();
            }
        }

        private void SetAllShardsAvailable(bool available)
        {
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] != null)
                    shards[i].SetAvailable(available);
            }
        }

        private static void EnsureSafePlayerSpawn()
        {
            PlayerInteractor player = FindObjectOfType<PlayerInteractor>();
            if (player == null || player.transform.position.y >= -3.2f)
                return;

            Vector3 position = player.transform.position;
            position.y = -2.85f;
            player.transform.position = position;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
                body.velocity = Vector2.zero;
        }

        private void PlayFeedback(AudioClip clipOverride, string resourceId)
        {
            if (clipOverride != null)
            {
                if (localAudioSource == null)
                {
                    localAudioSource = GetComponent<AudioSource>();
                    if (localAudioSource == null)
                        localAudioSource = gameObject.AddComponent<AudioSource>();
                    localAudioSource.playOnAwake = false;
                }

                localAudioSource.PlayOneShot(clipOverride);
                return;
            }

            if (!string.IsNullOrWhiteSpace(resourceId) && GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXById(resourceId.Trim());
        }

        private static Sprite CreateFallbackSprite()
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Level3FallbackTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
        }

        private static bool IsTargetScene()
        {
            return string.Equals(
                SceneManager.GetActiveScene().name,
                TargetSceneName,
                System.StringComparison.Ordinal);
        }
    }

    public static class Level3PuzzleEvents
    {
        public const string StageChanged = "Gameplay.Level3.StageChanged";
        public const string SequenceReset = "Gameplay.Level3.SequenceReset";
    }
}

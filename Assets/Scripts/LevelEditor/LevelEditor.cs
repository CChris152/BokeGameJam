using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using BokeGameJam.Core;
using BokeGameJam.Gameplay;
using BokeGameJam.Input;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BokeGameJam.LevelEditor
{
    /// <summary>
    /// 运行时关卡编辑器：完全事件驱动，不直接读 Unity Input。
    /// 支持双世界（A/B）+ 共享层：Shift 切换 A/B。
    /// 放置目标由 prefab 上 LevelObject.LevelLayer 决定（可在面板覆盖）。
    /// 画笔自动扫描 Assets/Prefabs/Terrians（tileId = 相对路径，如 Ground/BGround）。
    ///
    /// 交互：
    ///   M 键                — 切换 编辑 / 游玩 模式
    ///   Shift               — 切换世界 A / B
    ///   编辑模式下：
    ///     WASD/方向键       — 移动相机（由 CameraManager 处理）
    ///     鼠标左键 / 右键   — 放置 / 删除（按 LevelLayer 进 A / B / Shared）
    ///     Alt + 左键拖拽    — 移动共享层物体
    /// </summary>
    public sealed class LevelEditor : MonoBehaviour
    {
        private const float InactiveEditAlpha = 0.35f;

        /// <summary>仓库内地图目录（相对 Application.dataPath）。</summary>
        private const string LevelsFolderRelative = "Resources/Levels";

        /// <summary>Resources.Load 用的子路径（不含扩展名）。</summary>
        private const string LevelsResourcesPath = "Levels";

        /// <summary>地块画笔目录（相对项目根）。</summary>
        private const string TerrainPrefabsFolder = "Assets/Prefabs/Terrians";

        /// <summary>可交互物体相对路径前缀（tileId 以该前缀开头时显示机制配置）。</summary>
        private const string InteractableFolderPrefix = "Interactable/";

        [System.Serializable]
        public class TilePaletteEntry
        {
            public string tileId;
            public GameObject prefab;
        }

        [Header("Palette")]
        [SerializeField] private List<TilePaletteEntry> tilePalette = new();
        [SerializeField] private int currentPaletteIndex;

        [Header("Grid")]
        [Tooltip("网格单元大小，通常等于地块预制体的尺寸")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;

        [Header("References")]
        [SerializeField] private Camera mainCamera;

        [Tooltip("旧版单 root；若 tilesRootA 为空则回退使用此字段")]
        [SerializeField] private Transform tilesRoot;

        [SerializeField] private Transform tilesRootA;
        [SerializeField] private Transform tilesRootB;

        [Tooltip("共享层：Interactable 等不随 A/B 切换的物体")]
        [SerializeField] private Transform tilesRootShared;

        [Tooltip("进入编辑模式时会禁用该玩家脚本，避免残留物理速度；留空则由 InputContext 自动屏蔽输入")]
        [SerializeField] private PlayerController playerToDisable;

        [Header("Editor State")]
        [SerializeField] private bool showGridGizmos = true;

        [Header("Save / Load")]
        [Tooltip("命名规范：留空则用【当前场景名】做文件名（推荐）。\n" +
                 "只有当同一场景需要多张地图时，才在这里填一个自定义名字覆盖。")]
        [SerializeField] private string overrideFileName = string.Empty;

        [Tooltip("文件扩展名（含点），例如 .json")]
        [SerializeField] private string fileExtension = ".json";

        [Tooltip("进入场景时若存在同名地图则自动加载")]
        [SerializeField] private bool autoLoadOnStart = true;

        private readonly Dictionary<Vector2Int, PlacedTile> placedTilesA = new();
        private readonly Dictionary<Vector2Int, PlacedTile> placedTilesB = new();
        private readonly Dictionary<Vector2Int, PlacedTile> placedTilesShared = new();
        private bool isEditMode;
        private WorldId activeWorld = WorldId.A;
        private Rect guiPanelRect = new(12, 48, 300, 520);
        private Vector2 paletteScroll;
        private bool showHelp = true;

        // 放置参数
        private LevelLayer paintLevelLayer = LevelLayer.Shared;
        private string paintMechanismId = string.Empty;
        private string paintSequenceGroupId = string.Empty;
        private string paintSequenceIndexText = "0";
        private string paintDialogueText = string.Empty;

        // 光标预览
        private GameObject cursorPreview;
        private string cursorPreviewTileId;

        // 共享层拖拽移动
        private bool isDraggingShared;
        private Vector2Int dragSourceCell;
        private Vector2Int dragHoverCell;
        private PlacedTile dragTile;

        // 状态提示
        private string statusMessage;
        private float statusMessageExpireTime;

        private struct PlacedTile
        {
            public string tileId;
            public GameObject instance;
        }

        public bool IsEditMode => isEditMode;
        public WorldId ActiveWorld => activeWorld;

        /// <summary>当前生效的文件基础名（不含扩展名）：优先 override，否则当前场景名。</summary>
        public string CurrentLevelName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(overrideFileName))
                    return overrideFileName.Trim();
                string scene = SceneManager.GetActiveScene().name;
                return string.IsNullOrEmpty(scene) ? "Untitled" : scene;
            }
        }

        /// <summary>
        /// 完整保存路径 = Assets/Resources/Levels/{CurrentLevelName}{fileExtension}。
        /// 放在仓库内，便于 Git 同步；编辑器下可直接读写。
        /// </summary>
        public string SaveFilePath =>
            Path.Combine(Application.dataPath, LevelsFolderRelative, CurrentLevelName + fileExtension);

        /// <summary>Resources.Load 路径（不含扩展名），例如 Levels/Level1。</summary>
        public string ResourcesLoadPath =>
            LevelsResourcesPath + "/" + CurrentLevelName;

        private Dictionary<Vector2Int, PlacedTile> CurrentWorldPlacedTiles =>
            activeWorld == WorldId.A ? placedTilesA : placedTilesB;

        private Transform CurrentWorldTilesRoot =>
            activeWorld == WorldId.A ? tilesRootA : tilesRootB;

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            RebuildPaletteFromFolder();
            EnsureTileRoots();
            WorldManager.EnsureExists();
        }

        /// <summary>
        /// 扫描 Terrians 目录下所有 prefab 作为画笔。
        /// tileId = 相对路径（不含扩展名），例如 Ground/BGround、Interactable/PickableObject。
        /// 仅 Editor 可用（关卡编辑器本身也只在 Editor 里用）。
        /// </summary>
        private void RebuildPaletteFromFolder()
        {
#if UNITY_EDITOR
            tilePalette.Clear();

            string folderPrefix = TerrainPrefabsFolder.Replace('\\', '/') + "/";
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TerrainPrefabsFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                string relative = path.StartsWith(folderPrefix)
                    ? path.Substring(folderPrefix.Length)
                    : Path.GetFileName(path);
                if (relative.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    relative = relative.Substring(0, relative.Length - ".prefab".Length);

                tilePalette.Add(new TilePaletteEntry
                {
                    tileId = relative,
                    prefab = prefab,
                });
            }

            tilePalette.Sort((a, b) => string.CompareOrdinal(a.tileId, b.tileId));
            currentPaletteIndex = 0;
            SyncPaintLayerFromBrush();
            Debug.Log($"[LevelEditor] 已从 {TerrainPrefabsFolder} 加载 {tilePalette.Count} 个画笔");
#else
            Debug.LogWarning("[LevelEditor] 画笔扫描仅在 Unity Editor 下可用");
#endif
        }

        /// <summary>从 tileId（相对路径）取出文件夹部分；根目录返回空串。</summary>
        private static string GetTileFolder(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return string.Empty;

            int slash = tileId.LastIndexOf('/');
            return slash >= 0 ? tileId.Substring(0, slash) : string.Empty;
        }

        /// <summary>从 tileId（相对路径）取出文件名部分。</summary>
        private static string GetTileDisplayName(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return string.Empty;

            int slash = tileId.LastIndexOf('/');
            return slash >= 0 ? tileId.Substring(slash + 1) : tileId;
        }

        private void EnsureTileRoots()
        {
            if (tilesRootA == null && tilesRoot != null)
                tilesRootA = tilesRoot;

            if (tilesRootA == null)
            {
                GameObject rootA = new("_TilesRoot_A");
                tilesRootA = rootA.transform;
            }

            if (tilesRootB == null)
            {
                GameObject rootB = new("_TilesRoot_B");
                tilesRootB = rootB.transform;
            }

            if (tilesRootShared == null)
            {
                GameObject rootShared = new("_TilesRoot_Shared");
                tilesRootShared = rootShared.transform;
            }
        }

        private void OnEnable()
        {
            EventManager.On(InputEvents.EditorToggle, OnToggleRequest);
            EventManager.On(InputEvents.EditorSave, Save);
            EventManager.On(InputEvents.EditorLoad, Load);
            EventManager.On(InputEvents.EditorClear, ClearAllTiles);
            EventManager.On(InputEvents.EditorPaintHeld, OnPaintHeld);
            EventManager.On(InputEvents.EditorEraseHeld, OnEraseHeld);
            EventManager.On<int>(InputEvents.EditorSelectPalette, OnSelectPalette);
            EventManager.On<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);
        }

        private void OnDisable()
        {
            EventManager.Off(InputEvents.EditorToggle, OnToggleRequest);
            EventManager.Off(InputEvents.EditorSave, Save);
            EventManager.Off(InputEvents.EditorLoad, Load);
            EventManager.Off(InputEvents.EditorClear, ClearAllTiles);
            EventManager.Off(InputEvents.EditorPaintHeld, OnPaintHeld);
            EventManager.Off(InputEvents.EditorEraseHeld, OnEraseHeld);
            EventManager.Off<int>(InputEvents.EditorSelectPalette, OnSelectPalette);
            EventManager.Off<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);
        }

        private void Start()
        {
            string scene = SceneManager.GetActiveScene().name;
            Debug.Log($"[LevelEditor] 场景 '{scene}' → 地图文件 '{Path.GetFileName(SaveFilePath)}'");

            if (WorldManager.Instance != null)
                activeWorld = WorldManager.Instance.ActiveWorld;

            if (autoLoadOnStart && File.Exists(SaveFilePath))
                LoadSilent();

            SetEditMode(false);
            RefreshWorldVisibility();
        }

        private void Update()
        {
            if (!isEditMode)
            {
                CancelSharedDrag(restore: true);
                DestroyCursorPreview();
                return;
            }

            UpdateSharedDrag();
            UpdateCursorPreview();
        }

        // ---------- 事件回调 ----------

        private void OnToggleRequest() => SetEditMode(!isEditMode);

        private void OnActiveWorldChanged(WorldId world)
        {
            activeWorld = world;
            DestroyCursorPreview();
            RefreshWorldVisibility();
            SetStatus($"切换到世界 {WorldLabel(world)}");
        }

        private void OnPaintHeld()
        {
            if (!isEditMode || IsMouseOverGuiPanel() || isDraggingShared || IsAltHeld())
                return;
            if (TryGetCursorCell(out Vector2Int cell))
                PlaceTile(cell);
        }

        private void OnEraseHeld()
        {
            if (!isEditMode || IsMouseOverGuiPanel() || isDraggingShared)
                return;
            if (TryGetCursorCell(out Vector2Int cell))
                RemoveTile(cell);
        }

        private static bool IsAltHeld()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftAlt)
                || UnityEngine.Input.GetKey(KeyCode.RightAlt);
        }

        // ---------- 共享层拖拽 ----------

        private void UpdateSharedDrag()
        {
            if (isDraggingShared)
            {
                if (!UnityEngine.Input.GetMouseButton(0) || IsMouseOverGuiPanel())
                {
                    CommitSharedDrag();
                    return;
                }

                if (!TryGetCursorCell(out Vector2Int cell))
                    return;

                dragHoverCell = cell;
                if (dragTile.instance != null)
                    dragTile.instance.transform.position = CellToWorld(cell);
                return;
            }

            if (!IsAltHeld() || !UnityEngine.Input.GetMouseButtonDown(0) || IsMouseOverGuiPanel())
                return;

            if (!TryGetCursorCell(out Vector2Int source) || !placedTilesShared.TryGetValue(source, out PlacedTile tile))
                return;

            if (tile.instance == null)
                return;

            isDraggingShared = true;
            dragSourceCell = source;
            dragHoverCell = source;
            dragTile = tile;
            placedTilesShared.Remove(source);
            DestroyCursorPreview();
            SetStatus($"移动共享层: {GetTileDisplayName(tile.tileId)}");
        }

        private void CommitSharedDrag()
        {
            if (!isDraggingShared)
                return;

            Vector2Int target = dragHoverCell;
            PlacedTile moving = dragTile;
            isDraggingShared = false;
            dragTile = default;

            if (moving.instance == null)
                return;

            // 目标格已有共享物体：互换
            if (placedTilesShared.TryGetValue(target, out PlacedTile occupant) && target != dragSourceCell)
            {
                if (occupant.instance != null)
                    occupant.instance.transform.position = CellToWorld(dragSourceCell);
                placedTilesShared[dragSourceCell] = occupant;
            }

            // 目标格世界层清掉，保持互斥
            RemoveFromMap(placedTilesA, target);
            RemoveFromMap(placedTilesB, target);

            moving.instance.transform.position = CellToWorld(target);
            moving.instance.name = $"Tile_Shared_{moving.tileId}_{target.x}_{target.y}";
            placedTilesShared[target] = moving;

            if (target != dragSourceCell)
                SetStatus($"已移动到 ({target.x}, {target.y})");
        }

        private void CancelSharedDrag(bool restore)
        {
            if (!isDraggingShared)
                return;

            PlacedTile moving = dragTile;
            isDraggingShared = false;
            dragTile = default;

            if (!restore || moving.instance == null)
                return;

            moving.instance.transform.position = CellToWorld(dragSourceCell);
            placedTilesShared[dragSourceCell] = moving;
        }

        private void OnSelectPalette(int index)
        {
            if (!isEditMode || index < 0 || index >= tilePalette.Count)
                return;

            currentPaletteIndex = index;
            SyncPaintLayerFromBrush();
            DestroyCursorPreview();
        }

        // ---------- 模式切换 ----------

        public void SetEditMode(bool value)
        {
            if (!value)
                CancelSharedDrag(restore: true);

            isEditMode = value;

            if (playerToDisable != null)
                playerToDisable.enabled = !value;

            if (InputManager.Instance != null)
                InputManager.Instance.SetContext(value ? InputContext.LevelEditor : InputContext.Gameplay);

            RefreshWorldVisibility();
            SetStatus(value ? "已进入编辑模式" : "已退出编辑模式");
        }

        private bool IsMouseOverGuiPanel()
        {
            Vector2 pos = UnityEngine.Input.mousePosition;
            pos.y = Screen.height - pos.y;
            return guiPanelRect.Contains(pos);
        }

        // ---------- 世界可见性 ----------

        private void RefreshWorldVisibility()
        {
            if (tilesRootA == null || tilesRootB == null)
                return;

            // 共享层始终可见、始终可碰撞
            if (tilesRootShared != null)
            {
                tilesRootShared.gameObject.SetActive(true);
                ApplyLayerPresentation(tilesRootShared, isActiveLayer: true);
            }

            if (isEditMode)
            {
                tilesRootA.gameObject.SetActive(true);
                tilesRootB.gameObject.SetActive(true);
                ApplyLayerPresentation(tilesRootA, isActiveLayer: activeWorld == WorldId.A);
                ApplyLayerPresentation(tilesRootB, isActiveLayer: activeWorld == WorldId.B);
            }
            else
            {
                bool showA = activeWorld == WorldId.A;
                tilesRootA.gameObject.SetActive(showA);
                tilesRootB.gameObject.SetActive(!showA);

                if (showA)
                    ApplyLayerPresentation(tilesRootA, isActiveLayer: true);
                else
                    ApplyLayerPresentation(tilesRootB, isActiveLayer: true);
            }
        }

        private static void ApplyLayerPresentation(Transform root, bool isActiveLayer)
        {
            if (root == null)
                return;

            float alpha = isActiveLayer ? 1f : InactiveEditAlpha;
            SetRootTransparency(root, alpha);
            SetRootCollidersEnabled(root, isActiveLayer);
        }

        private static void SetRootTransparency(Transform root, float alpha)
        {
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        private static void SetRootCollidersEnabled(Transform root, bool enabled)
        {
            foreach (Collider2D c in root.GetComponentsInChildren<Collider2D>(true))
                c.enabled = enabled;

            foreach (Rigidbody2D rb in root.GetComponentsInChildren<Rigidbody2D>(true))
                rb.simulated = enabled;
        }

        // ---------- 网格 & 光标 ----------

        private bool TryGetCursorCell(out Vector2Int cell)
        {
            cell = default;
            if (mainCamera == null)
                return false;

            Vector3 mouse = UnityEngine.Input.mousePosition;
            mouse.z = Mathf.Abs(mainCamera.transform.position.z);
            Vector3 world = mainCamera.ScreenToWorldPoint(mouse);
            cell = WorldToCell(world);
            return true;
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            float safeSize = Mathf.Max(0.0001f, cellSize);
            int x = Mathf.FloorToInt((world.x - gridOrigin.x) / safeSize + 0.5f);
            int y = Mathf.FloorToInt((world.y - gridOrigin.y) / safeSize + 0.5f);
            return new Vector2Int(x, y);
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(gridOrigin.x + cell.x * cellSize, gridOrigin.y + cell.y * cellSize, 0f);
        }

        private void UpdateCursorPreview()
        {
            TilePaletteEntry entry = GetCurrentEntry();
            if (isDraggingShared || entry == null || entry.prefab == null || IsMouseOverGuiPanel())
            {
                DestroyCursorPreview();
                return;
            }

            if (cursorPreview == null || cursorPreviewTileId != entry.tileId)
            {
                DestroyCursorPreview();
                cursorPreview = Instantiate(entry.prefab);
                cursorPreview.name = $"__CursorPreview_{entry.tileId}";
                cursorPreviewTileId = entry.tileId;
                SetPreviewTransparency(cursorPreview, 0.5f);
                DisableColliders(cursorPreview);
            }

            if (TryGetCursorCell(out Vector2Int cell))
                cursorPreview.transform.position = CellToWorld(cell);
        }

        private void DestroyCursorPreview()
        {
            if (cursorPreview != null)
                Destroy(cursorPreview);

            cursorPreview = null;
            cursorPreviewTileId = null;
        }

        private static void SetPreviewTransparency(GameObject go, float alpha)
        {
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        private static void DisableColliders(GameObject go)
        {
            foreach (Collider2D c in go.GetComponentsInChildren<Collider2D>(true))
                c.enabled = false;

            foreach (Rigidbody2D rb in go.GetComponentsInChildren<Rigidbody2D>(true))
                rb.simulated = false;
        }

        // ---------- 地块增删 ----------

        private TilePaletteEntry GetCurrentEntry()
        {
            if (tilePalette.Count == 0)
                return null;

            currentPaletteIndex = Mathf.Clamp(currentPaletteIndex, 0, tilePalette.Count - 1);
            return tilePalette[currentPaletteIndex];
        }

        public void PlaceTile(Vector2Int cell)
        {
            TilePaletteEntry entry = GetCurrentEntry();
            if (entry == null || entry.prefab == null)
                return;

            LevelLayer layer = ResolvePaintLayer(entry);
            if (!TryGetLayerContainers(layer, out Dictionary<Vector2Int, PlacedTile> map, out Transform root))
                return;

            ClearCellExcept(cell, layer);

            if (map.TryGetValue(cell, out PlacedTile existing))
            {
                // 同类型再刷：更新配置（含层级相关字段）
                if (existing.tileId == entry.tileId)
                {
                    if (existing.instance != null)
                        ApplyPaintConfigToInstance(existing.instance, layer);
                    return;
                }

                if (existing.instance != null)
                    Destroy(existing.instance);
            }

            string layerLabel = LayerLabel(layer);
            GameObject instance = Instantiate(entry.prefab, CellToWorld(cell), Quaternion.identity, root);
            instance.name = $"Tile_{layerLabel}_{entry.tileId}_{cell.x}_{cell.y}";
            ApplyPaintConfigToInstance(instance, layer);
            map[cell] = new PlacedTile { tileId = entry.tileId, instance = instance };

            if (isEditMode)
            {
                bool active = layer == LevelLayer.Shared
                    || (layer == LevelLayer.A && activeWorld == WorldId.A)
                    || (layer == LevelLayer.B && activeWorld == WorldId.B);
                ApplyLayerPresentation(root, isActiveLayer: active);
            }
        }

        /// <summary>
        /// 放置前清理冲突格：A/B 地块可同格共存；Shared 与世界层互斥。
        /// </summary>
        private void ClearCellExcept(Vector2Int cell, LevelLayer keepLayer)
        {
            if (keepLayer == LevelLayer.Shared)
            {
                RemoveFromMap(placedTilesA, cell);
                RemoveFromMap(placedTilesB, cell);
                return;
            }

            // 放置 A 或 B：只清 Shared，不碰另一世界层
            RemoveFromMap(placedTilesShared, cell);
        }

        private static void RemoveFromMap(Dictionary<Vector2Int, PlacedTile> map, Vector2Int cell)
        {
            if (map == null || !map.TryGetValue(cell, out PlacedTile tile))
                return;

            if (tile.instance != null)
                Destroy(tile.instance);

            map.Remove(cell);
        }

        private bool TryGetLayerContainers(
            LevelLayer layer,
            out Dictionary<Vector2Int, PlacedTile> map,
            out Transform root)
        {
            switch (layer)
            {
                case LevelLayer.A:
                    map = placedTilesA;
                    root = tilesRootA;
                    break;
                case LevelLayer.B:
                    map = placedTilesB;
                    root = tilesRootB;
                    break;
                default:
                    map = placedTilesShared;
                    root = tilesRootShared;
                    break;
            }

            return root != null && map != null;
        }

        private static string LayerLabel(LevelLayer layer)
        {
            return layer switch
            {
                LevelLayer.A => "A",
                LevelLayer.B => "B",
                _ => "Shared",
            };
        }

        private static bool IsInteractableTileId(string tileId)
        {
            return !string.IsNullOrEmpty(tileId)
                && tileId.StartsWith(InteractableFolderPrefix, System.StringComparison.Ordinal);
        }

        private bool CurrentBrushIsInteractable()
        {
            TilePaletteEntry entry = GetCurrentEntry();
            return entry != null && entry.prefab != null
                && entry.prefab.GetComponent<InteractableObject>() != null;
        }

        private bool CurrentBrushHasSequenceFields()
        {
            TilePaletteEntry entry = GetCurrentEntry();
            return entry != null && entry.prefab != null
                && entry.prefab.GetComponent<InteractableObjectB>() != null;
        }

        private bool CurrentBrushIsGhost()
        {
            TilePaletteEntry entry = GetCurrentEntry();
            return entry != null && entry.prefab != null
                && entry.prefab.GetComponent<InteractableObjectD>() != null;
        }

        private LevelLayer ResolvePaintLayer(TilePaletteEntry entry)
        {
            if (entry == null)
                return paintLevelLayer;

            // 面板选择优先；无 LevelObject 时按路径回退
            LevelObject levelObject = entry.prefab != null
                ? entry.prefab.GetComponent<LevelObject>()
                : null;
            if (levelObject != null)
                return paintLevelLayer;

            if (IsInteractableTileId(entry.tileId))
                return LevelLayer.Shared;

            return activeWorld == WorldId.A ? LevelLayer.A : LevelLayer.B;
        }

        private void SyncPaintLayerFromBrush()
        {
            TilePaletteEntry entry = GetCurrentEntry();
            if (entry?.prefab == null)
                return;

            LevelObject levelObject = entry.prefab.GetComponent<LevelObject>();
            if (levelObject != null)
            {
                paintLevelLayer = levelObject.LevelLayer;
                return;
            }

            paintLevelLayer = IsInteractableTileId(entry.tileId)
                ? LevelLayer.Shared
                : (activeWorld == WorldId.A ? LevelLayer.A : LevelLayer.B);
        }

        private int GetPaintSequenceIndex()
        {
            return int.TryParse(paintSequenceIndexText, out int value) ? value : 0;
        }

        private void ApplyPaintConfigToInstance(GameObject instance, LevelLayer layer)
        {
            if (instance == null)
                return;

            LevelObject levelObject = instance.GetComponent<LevelObject>();
            if (levelObject != null)
                levelObject.SetLevelLayer(layer);

            InteractableObject interactable = instance.GetComponent<InteractableObject>();
            if (interactable == null)
                return;

            interactable.ApplyEditorConfig(
                paintMechanismId,
                paintSequenceGroupId,
                GetPaintSequenceIndex());

            InteractableObjectD ghost = instance.GetComponent<InteractableObjectD>();
            if (ghost != null)
                ghost.ApplyDialogueText(paintDialogueText);
        }

        private static void ApplyTileEntryConfig(GameObject instance, LevelData.TileEntry entry, LevelLayer layer)
        {
            if (instance == null)
                return;

            LevelObject levelObject = instance.GetComponent<LevelObject>();
            if (levelObject != null)
                levelObject.SetLevelLayer(layer);

            InteractableObject interactable = instance.GetComponent<InteractableObject>();
            if (interactable == null)
                return;

            interactable.ApplyEditorConfig(
                entry.mechanismId,
                entry.sequenceGroupId,
                entry.sequenceIndex);

            InteractableObjectD ghost = instance.GetComponent<InteractableObjectD>();
            if (ghost != null)
                ghost.ApplyDialogueText(entry.dialogueText);
        }

        private static LevelData.TileEntry CaptureTileEntry(Vector2Int cell, PlacedTile tile)
        {
            string mechanismId = null;
            string sequenceGroupId = null;
            int sequenceIndex = 0;
            string dialogueText = null;

            if (tile.instance != null)
            {
                InteractableObjectD ghost = tile.instance.GetComponent<InteractableObjectD>();
                if (ghost != null)
                {
                    mechanismId = ghost.MechanismId;
                    dialogueText = ghost.DialogueText;
                }
                else
                {
                    InteractableObjectB b = tile.instance.GetComponent<InteractableObjectB>();
                    if (b != null)
                    {
                        mechanismId = b.MechanismId;
                        sequenceGroupId = b.SequenceGroupId;
                        sequenceIndex = b.SequenceIndex;
                    }
                    else
                    {
                        InteractableObject interactable = tile.instance.GetComponent<InteractableObject>();
                        if (interactable != null)
                            mechanismId = interactable.MechanismId;
                    }
                }
            }

            return new LevelData.TileEntry(
                cell.x,
                cell.y,
                tile.tileId,
                mechanismId,
                sequenceGroupId,
                sequenceIndex,
                dialogueText);
        }

        public void RemoveTile(Vector2Int cell)
        {
            // 优先删共享层（Interactable 叠在上面），否则删当前世界层
            if (placedTilesShared.ContainsKey(cell))
                RemoveFromMap(placedTilesShared, cell);
            else
                RemoveFromMap(CurrentWorldPlacedTiles, cell);
        }

        /// <summary>清空当前画笔所属层级。</summary>
        public void ClearAllTiles()
        {
            LevelLayer layer = ResolvePaintLayer(GetCurrentEntry());
            if (!TryGetLayerContainers(layer, out Dictionary<Vector2Int, PlacedTile> map, out _))
                return;

            ClearMap(map);
            SetStatus($"已清空层级 {LayerLabel(layer)}");
        }

        private void ClearWorldTiles(WorldId world)
        {
            ClearMap(world == WorldId.A ? placedTilesA : placedTilesB);
        }

        private static void ClearMap(Dictionary<Vector2Int, PlacedTile> map)
        {
            if (map == null)
                return;

            foreach (PlacedTile t in map.Values)
            {
                if (t.instance != null)
                    Destroy(t.instance);
            }

            map.Clear();
        }

        private void ClearAllLayers()
        {
            ClearWorldTiles(WorldId.A);
            ClearWorldTiles(WorldId.B);
            ClearMap(placedTilesShared);
        }

        // ---------- 保存 / 加载 ----------

        public void Save()
        {
            LevelData data = new()
            {
                version = 2,
                levelName = CurrentLevelName,
            };

            AppendTiles(data.tilesA, placedTilesA);
            AppendTiles(data.tilesB, placedTilesB);
            AppendTiles(data.tilesShared, placedTilesShared);

            try
            {
                string path = SaveFilePath;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(path, LevelData.ToJson(data));
                SetStatus(
                    $"已保存 A:{data.tilesA.Count} B:{data.tilesB.Count} S:{data.tilesShared.Count} → {Path.GetFileName(path)}");
                Debug.Log(
                    $"[LevelEditor] 已保存 A={data.tilesA.Count} B={data.tilesB.Count} Shared={data.tilesShared.Count} → {path}");
            }
            catch (System.Exception ex)
            {
                SetStatus($"保存失败: {ex.Message}");
                Debug.LogError($"[LevelEditor] 保存失败: {ex.Message}");
            }
        }

        private static void AppendTiles(List<LevelData.TileEntry> list, Dictionary<Vector2Int, PlacedTile> map)
        {
            foreach (KeyValuePair<Vector2Int, PlacedTile> kv in map)
                list.Add(CaptureTileEntry(kv.Key, kv.Value));
        }

        /// <summary>手动加载：找不到文件会弹警告。</summary>
        public void Load()
        {
            if (!TryLoadLevel())
            {
                SetStatus($"未找到地图: {CurrentLevelName}{fileExtension}");
                Debug.LogWarning(
                    $"[LevelEditor] 未找到关卡文件: {SaveFilePath} 或 Resources/{ResourcesLoadPath}");
            }
        }

        /// <summary>自动加载：找不到文件静默返回，用于场景启动时。</summary>
        public void LoadSilent()
        {
            TryLoadLevel();
        }

        /// <summary>
        /// 优先读仓库内 JSON 文件；打包后文件可能不在磁盘上，再回退 Resources.Load。
        /// </summary>
        private bool TryLoadLevel()
        {
            string path = SaveFilePath;
            if (File.Exists(path))
            {
                LoadFromJson(File.ReadAllText(path), path);
                return true;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourcesLoadPath);
            if (asset != null)
            {
                LoadFromJson(asset.text, $"Resources/{ResourcesLoadPath}");
                return true;
            }

            return false;
        }

        private void LoadFromJson(string json, string sourceLabel)
        {
            try
            {
                LevelData data = LevelData.FromJson(json);
                ApplyLevelData(data);
                int sharedCount = data.tilesShared?.Count ?? 0;
                SetStatus(
                    $"已加载 A:{data.tilesA.Count} B:{data.tilesB.Count} S:{sharedCount} ← {Path.GetFileName(sourceLabel)}");
                Debug.Log(
                    $"[LevelEditor] 已加载 A={data.tilesA.Count} B={data.tilesB.Count} Shared={sharedCount} ← {sourceLabel}");
            }
            catch (System.Exception ex)
            {
                SetStatus($"加载失败: {ex.Message}");
                Debug.LogError($"[LevelEditor] 加载失败 ({sourceLabel}): {ex.Message}");
            }
        }

        /// <summary>把 LevelData 应用到场景（会先清空全部层）。</summary>
        public void ApplyLevelData(LevelData data)
        {
            ClearAllLayers();
            if (data == null)
                return;

            LevelData.Normalize(data);

            int savedIndex = currentPaletteIndex;
            SpawnTiles(data.tilesA, placedTilesA, tilesRootA, LevelLayer.A);
            SpawnTiles(data.tilesB, placedTilesB, tilesRootB, LevelLayer.B);
            SpawnTiles(data.tilesShared, placedTilesShared, tilesRootShared, LevelLayer.Shared);
            currentPaletteIndex = Mathf.Clamp(savedIndex, 0, Mathf.Max(0, tilePalette.Count - 1));
            SyncPaintLayerFromBrush();

            RefreshWorldVisibility();
        }

        private void SpawnTiles(
            List<LevelData.TileEntry> entries,
            Dictionary<Vector2Int, PlacedTile> map,
            Transform root,
            LevelLayer layer)
        {
            if (entries == null || map == null || root == null)
                return;

            string layerLabel = LayerLabel(layer);
            foreach (LevelData.TileEntry entry in entries)
            {
                int idx = FindPaletteIndex(entry.tileId);
                if (idx < 0)
                {
                    Debug.LogWarning($"[LevelEditor] 找不到 tileId: {entry.tileId}，跳过");
                    continue;
                }

                TilePaletteEntry palette = tilePalette[idx];
                if (palette == null || palette.prefab == null)
                    continue;

                Vector2Int cell = new(entry.x, entry.y);
                if (map.TryGetValue(cell, out PlacedTile existing) && existing.instance != null)
                    Destroy(existing.instance);

                GameObject instance = Instantiate(palette.prefab, CellToWorld(cell), Quaternion.identity, root);
                instance.name = $"Tile_{layerLabel}_{entry.tileId}_{cell.x}_{cell.y}";
                ApplyTileEntryConfig(instance, entry, layer);
                map[cell] = new PlacedTile { tileId = entry.tileId, instance = instance };
            }
        }

        private int FindPaletteIndex(string tileId)
        {
            for (int i = 0; i < tilePalette.Count; i++)
            {
                if (tilePalette[i] != null && tilePalette[i].tileId == tileId)
                    return i;
            }
            return -1;
        }

        private static string WorldLabel(WorldId world) => world == WorldId.A ? "A" : "B";

        // ---------- 图形界面 ----------

        private void SetStatus(string message, float duration = 3f)
        {
            statusMessage = message;
            statusMessageExpireTime = Time.unscaledTime + duration;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawModeBadge();

            if (!isEditMode)
                return;

            guiPanelRect = GUI.Window(9527, guiPanelRect, DrawEditorWindow, "关卡编辑器  ·  M 键退出");
        }

        private void DrawModeBadge()
        {
            const float w = 300f;
            const float h = 30f;
            Rect r = new(Screen.width - w - 12f, 12f, w, h);

            Color prev = GUI.color;
            GUI.color = isEditMode ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(0.2f, 0.9f, 0.4f, 0.95f);
            GUI.Box(r, GUIContent.none, badgeStyle);
            GUI.color = prev;

            string sceneName = SceneManager.GetActiveScene().name;
            string world = WorldLabel(activeWorld);
            string text = isEditMode
                ? $"● 编辑 · 世界{world} · {sceneName}  [M/Shift]"
                : $"● 游玩 · 世界{world} · {sceneName}  [M/Shift]";
            GUI.Label(new Rect(r.x, r.y, r.width, r.height), text, badgeLabelStyle);
        }

        private void DrawEditorWindow(int id)
        {
            GUILayout.Space(6);

            if (!string.IsNullOrEmpty(statusMessage) && Time.unscaledTime < statusMessageExpireTime)
            {
                GUILayout.BeginHorizontal(boxStyle);
                GUILayout.Label($"● {statusMessage}", statusLabelStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            GUILayout.Label($"当前世界: {WorldLabel(activeWorld)}  （Shift 切换）", headerStyle);
            GUILayout.Space(4);

            GUILayout.Label("地块", headerStyle);
            paletteScroll = GUILayout.BeginScrollView(paletteScroll, boxStyle, GUILayout.Height(180));
            if (tilePalette.Count == 0)
            {
                GUILayout.Label("（未找到任何地块）", mutedStyle);
            }
            else
            {
                string lastFolder = null;
                for (int i = 0; i < tilePalette.Count; i++)
                {
                    TilePaletteEntry entry = tilePalette[i];
                    if (entry == null) continue;

                    string folder = GetTileFolder(entry.tileId);
                    if (folder != lastFolder)
                    {
                        lastFolder = folder;
                        string folderLabel = string.IsNullOrEmpty(folder) ? "（根目录）" : folder;
                        GUILayout.Space(2);
                        GUILayout.Label($"▸ {folderLabel}", headerStyle);
                    }

                    bool selected = i == currentPaletteIndex;
                    GUIStyle style = selected ? paletteSelectedStyle : paletteButtonStyle;
                    string prefix = selected ? "▶ " : "    ";
                    string hotkey = i < 9 ? $"[{i + 1}] " : "     ";
                    string displayName = GetTileDisplayName(entry.tileId);
                    if (GUILayout.Button($"{prefix}{hotkey}{displayName}", style))
                    {
                        currentPaletteIndex = i;
                        SyncPaintLayerFromBrush();
                        DestroyCursorPreview();
                    }
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("所属层级", headerStyle);
            GUILayout.BeginHorizontal(boxStyle);
            if (GUILayout.Toggle(paintLevelLayer == LevelLayer.A, "A", GUILayout.Width(50)))
                paintLevelLayer = LevelLayer.A;
            if (GUILayout.Toggle(paintLevelLayer == LevelLayer.B, "B", GUILayout.Width(50)))
                paintLevelLayer = LevelLayer.B;
            if (GUILayout.Toggle(paintLevelLayer == LevelLayer.Shared, "Shared", GUILayout.Width(70)))
                paintLevelLayer = LevelLayer.Shared;
            GUILayout.EndHorizontal();
            GUILayout.Label("放置进所选层级；prefab 默认值会在切换画笔时同步", mutedStyle);

            if (CurrentBrushIsInteractable())
            {
                GUILayout.Space(6);
                GUILayout.Label("Interactable 配置", headerStyle);
                GUILayout.BeginVertical(boxStyle);

                GUILayout.BeginHorizontal();
                GUILayout.Label("mechanismId", GUILayout.Width(110));
                paintMechanismId = GUILayout.TextField(paintMechanismId ?? string.Empty);
                GUILayout.EndHorizontal();

                if (CurrentBrushHasSequenceFields())
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("sequenceGroupId", GUILayout.Width(110));
                    paintSequenceGroupId = GUILayout.TextField(paintSequenceGroupId ?? string.Empty);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("sequenceIndex", GUILayout.Width(110));
                    paintSequenceIndexText = GUILayout.TextField(paintSequenceIndexText ?? "0");
                    GUILayout.EndHorizontal();

                    GUILayout.Label("同类型再刷可更新已放置物体的配置", mutedStyle);
                }
                else if (CurrentBrushIsGhost())
                {
                    GUILayout.Label("dialogueText", mutedStyle);
                    paintDialogueText = GUILayout.TextArea(paintDialogueText ?? string.Empty, GUILayout.Height(54));
                    GUILayout.Label("D（鬼魂）：可反复对话；每关建议只放一个", mutedStyle);
                }
                else
                {
                    GUILayout.Label("A/C：只需 mechanismId；同类型再刷可更新", mutedStyle);
                }

                GUILayout.EndVertical();
            }

            GUILayout.Space(8);

            GUILayout.Label("文件", headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存", GUILayout.Height(26))) Save();
            if (GUILayout.Button("加载", GUILayout.Height(26))) Load();
            GUILayout.EndHorizontal();
            if (GUILayout.Button($"清空层级 {LayerLabel(paintLevelLayer)}", GUILayout.Height(24))) ClearAllTiles();

            GUILayout.Space(6);

            showHelp = GUILayout.Toggle(showHelp, showHelp ? "▼ 操作提示" : "▶ 操作提示", foldoutStyle);
            if (showHelp)
            {
                GUILayout.BeginVertical(boxStyle);
                GUILayout.Label("鼠标左键 · 放置地块", mutedStyle);
                GUILayout.Label("鼠标右键 · 删除地块", mutedStyle);
                GUILayout.Label("鼠标滚轮 · 缩放视野", mutedStyle);
                GUILayout.Label("WASD / 方向键 · 移动相机", mutedStyle);
                GUILayout.Label("Shift · 切换世界 A/B", mutedStyle);
                GUILayout.Label("所属层级 · A/B/Shared（prefab 可默认）", mutedStyle);
                GUILayout.Label("Alt + 左键拖拽 · 移动共享层物体", mutedStyle);
                GUILayout.Label("数字键 1-9 · 选择地块", mutedStyle);
                GUILayout.EndVertical();
            }

            GUILayout.Space(4);
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label($"场景: {SceneManager.GetActiveScene().name}", mutedStyle);
            GUILayout.Label($"地图: {Path.GetFileName(SaveFilePath)}", mutedStyle);
            GUILayout.Label(
                $"地块: A={placedTilesA.Count}  B={placedTilesB.Count}  S={placedTilesShared.Count}",
                mutedStyle);
            if (!string.IsNullOrWhiteSpace(overrideFileName))
                GUILayout.Label("（已用 overrideFileName 覆盖场景名）", mutedStyle);
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        // ---------- 样式 ----------

        private static GUIStyle richLabelStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle mutedStyle;
        private static GUIStyle boxStyle;
        private static GUIStyle badgeStyle;
        private static GUIStyle badgeLabelStyle;
        private static GUIStyle paletteButtonStyle;
        private static GUIStyle paletteSelectedStyle;
        private static GUIStyle statusLabelStyle;
        private static GUIStyle foldoutStyle;

        private static void EnsureStyles()
        {
            if (headerStyle != null) return;

            richLabelStyle = new GUIStyle(GUI.skin.label) { richText = true };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                padding = new RectOffset(2, 2, 2, 2),
            };
            headerStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);

            mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
            };
            mutedStyle.normal.textColor = new Color(0.75f, 0.75f, 0.78f);

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(6, 6, 4, 4),
            };

            badgeStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
            };

            badgeLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
            };
            badgeLabelStyle.normal.textColor = Color.black;

            paletteButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4),
            };

            paletteSelectedStyle = new GUIStyle(paletteButtonStyle);
            paletteSelectedStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
            paletteSelectedStyle.hover.textColor = new Color(1f, 0.9f, 0.4f);
            paletteSelectedStyle.fontStyle = FontStyle.Bold;

            statusLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
            };
            statusLabelStyle.normal.textColor = new Color(0.4f, 1f, 0.6f);

            foldoutStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
            };
        }

        private static GUIStyle RichLabelStyle()
        {
            if (richLabelStyle == null)
                richLabelStyle = new GUIStyle(GUI.skin.label) { richText = true };
            return richLabelStyle;
        }

        private void OnDrawGizmos()
        {
            if (!showGridGizmos)
                return;

            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            const int extent = 12;
            for (int x = -extent; x <= extent; x++)
            {
                Vector3 a = new(gridOrigin.x + (x - 0.5f) * cellSize, gridOrigin.y + (-extent - 0.5f) * cellSize, 0f);
                Vector3 b = new(gridOrigin.x + (x - 0.5f) * cellSize, gridOrigin.y + (extent + 0.5f) * cellSize, 0f);
                Gizmos.DrawLine(a, b);
            }
            for (int y = -extent; y <= extent; y++)
            {
                Vector3 a = new(gridOrigin.x + (-extent - 0.5f) * cellSize, gridOrigin.y + (y - 0.5f) * cellSize, 0f);
                Vector3 b = new(gridOrigin.x + (extent + 0.5f) * cellSize, gridOrigin.y + (y - 0.5f) * cellSize, 0f);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}

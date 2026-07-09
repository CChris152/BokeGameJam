using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using BokeGameJam.Core;
using BokeGameJam.Gameplay;
using BokeGameJam.Input;

namespace BokeGameJam.LevelEditor
{
    /// <summary>
    /// 运行时关卡编辑器：完全事件驱动，不直接读 Unity Input。
    /// 支持双世界（A/B）：Shift 切换，单文件双层存档。
    ///
    /// 交互：
    ///   M 键                — 切换 编辑 / 游玩 模式
    ///   Shift               — 切换世界 A / B
    ///   编辑模式下：
    ///     WASD/方向键       — 移动相机（由 CameraManager 处理）
    ///     鼠标左键 / 右键   — 放置 / 删除地块（仅当前世界）
    /// </summary>
    public sealed class LevelEditor : MonoBehaviour
    {
        private const float InactiveEditAlpha = 0.35f;

        [System.Serializable]
        public class TilePaletteEntry
        {
            [Tooltip("地块唯一标识，用于保存/加载时匹配预制体")]
            public string tileId = "ground";

            [Tooltip("对应的地块预制体")]
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
        private bool isEditMode;
        private WorldId activeWorld = WorldId.A;
        private Rect guiPanelRect = new(12, 48, 300, 440);
        private Vector2 paletteScroll;
        private bool showHelp = true;

        // 光标预览
        private GameObject cursorPreview;
        private string cursorPreviewTileId;

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

        /// <summary>完整保存路径 = persistentDataPath / {CurrentLevelName}{fileExtension}</summary>
        public string SaveFilePath =>
            Path.Combine(Application.persistentDataPath, CurrentLevelName + fileExtension);

        private Dictionary<Vector2Int, PlacedTile> CurrentPlacedTiles =>
            activeWorld == WorldId.A ? placedTilesA : placedTilesB;

        private Transform CurrentTilesRoot =>
            activeWorld == WorldId.A ? tilesRootA : tilesRootB;

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            EnsureTileRoots();
            WorldManager.EnsureExists();
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
                DestroyCursorPreview();
                return;
            }

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
            if (!isEditMode || IsMouseOverGuiPanel())
                return;
            if (TryGetCursorCell(out Vector2Int cell))
                PlaceTile(cell);
        }

        private void OnEraseHeld()
        {
            if (!isEditMode || IsMouseOverGuiPanel())
                return;
            if (TryGetCursorCell(out Vector2Int cell))
                RemoveTile(cell);
        }

        private void OnSelectPalette(int index)
        {
            if (!isEditMode || index < 0 || index >= tilePalette.Count)
                return;

            currentPaletteIndex = index;
            DestroyCursorPreview();
        }

        // ---------- 模式切换 ----------

        public void SetEditMode(bool value)
        {
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
            if (entry == null || entry.prefab == null || IsMouseOverGuiPanel())
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

            Dictionary<Vector2Int, PlacedTile> map = CurrentPlacedTiles;
            Transform root = CurrentTilesRoot;

            if (map.TryGetValue(cell, out PlacedTile existing))
            {
                if (existing.tileId == entry.tileId)
                    return;

                if (existing.instance != null)
                    Destroy(existing.instance);
            }

            GameObject instance = Instantiate(entry.prefab, CellToWorld(cell), Quaternion.identity, root);
            instance.name = $"Tile_{WorldLabel(activeWorld)}_{entry.tileId}_{cell.x}_{cell.y}";
            map[cell] = new PlacedTile { tileId = entry.tileId, instance = instance };

            // 新放置的地块在编辑对照层时需立刻套用当前层表现
            if (isEditMode)
                ApplyLayerPresentation(root, isActiveLayer: true);
        }

        public void RemoveTile(Vector2Int cell)
        {
            Dictionary<Vector2Int, PlacedTile> map = CurrentPlacedTiles;
            if (!map.TryGetValue(cell, out PlacedTile tile))
                return;

            if (tile.instance != null)
                Destroy(tile.instance);

            map.Remove(cell);
        }

        /// <summary>清空当前世界的地块。</summary>
        public void ClearAllTiles()
        {
            ClearWorldTiles(activeWorld);
            SetStatus($"已清空世界 {WorldLabel(activeWorld)}");
        }

        private void ClearWorldTiles(WorldId world)
        {
            Dictionary<Vector2Int, PlacedTile> map = world == WorldId.A ? placedTilesA : placedTilesB;
            foreach (PlacedTile t in map.Values)
            {
                if (t.instance != null)
                    Destroy(t.instance);
            }

            map.Clear();
        }

        private void ClearBothWorlds()
        {
            ClearWorldTiles(WorldId.A);
            ClearWorldTiles(WorldId.B);
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

            try
            {
                string path = SaveFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
                File.WriteAllText(path, LevelData.ToJson(data));
                SetStatus($"已保存 A:{data.tilesA.Count} B:{data.tilesB.Count} → {Path.GetFileName(path)}");
                Debug.Log($"[LevelEditor] 已保存 A={data.tilesA.Count} B={data.tilesB.Count} → {path}");
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
                list.Add(new LevelData.TileEntry(kv.Key.x, kv.Key.y, kv.Value.tileId));
        }

        /// <summary>手动加载：找不到文件会弹警告。</summary>
        public void Load()
        {
            string path = SaveFilePath;
            if (!File.Exists(path))
            {
                SetStatus($"未找到地图: {Path.GetFileName(path)}");
                Debug.LogWarning($"[LevelEditor] 未找到关卡文件: {path}");
                return;
            }

            LoadFromPath(path);
        }

        /// <summary>自动加载：找不到文件静默返回，用于场景启动时。</summary>
        public void LoadSilent()
        {
            string path = SaveFilePath;
            if (!File.Exists(path)) return;
            LoadFromPath(path);
        }

        private void LoadFromPath(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                LevelData data = LevelData.FromJson(json);
                ApplyLevelData(data);
                SetStatus($"已加载 A:{data.tilesA.Count} B:{data.tilesB.Count} ← {Path.GetFileName(path)}");
                Debug.Log($"[LevelEditor] 已加载 A={data.tilesA.Count} B={data.tilesB.Count} ← {path}");
            }
            catch (System.Exception ex)
            {
                SetStatus($"加载失败: {ex.Message}");
                Debug.LogError($"[LevelEditor] 加载失败: {ex.Message}");
            }
        }

        /// <summary>把 LevelData 应用到场景（会先清空双世界地块）。</summary>
        public void ApplyLevelData(LevelData data)
        {
            ClearBothWorlds();
            if (data == null)
                return;

            LevelData.Normalize(data);

            int savedIndex = currentPaletteIndex;
            SpawnTiles(data.tilesA, WorldId.A);
            SpawnTiles(data.tilesB, WorldId.B);
            currentPaletteIndex = Mathf.Clamp(savedIndex, 0, Mathf.Max(0, tilePalette.Count - 1));

            RefreshWorldVisibility();
        }

        private void SpawnTiles(List<LevelData.TileEntry> entries, WorldId world)
        {
            if (entries == null)
                return;

            Dictionary<Vector2Int, PlacedTile> map = world == WorldId.A ? placedTilesA : placedTilesB;
            Transform root = world == WorldId.A ? tilesRootA : tilesRootB;

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
                instance.name = $"Tile_{WorldLabel(world)}_{entry.tileId}_{cell.x}_{cell.y}";
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
            paletteScroll = GUILayout.BeginScrollView(paletteScroll, boxStyle, GUILayout.Height(140));
            if (tilePalette.Count == 0)
            {
                GUILayout.Label("（未配置任何地块）", mutedStyle);
            }
            else
            {
                for (int i = 0; i < tilePalette.Count; i++)
                {
                    TilePaletteEntry entry = tilePalette[i];
                    if (entry == null) continue;

                    bool selected = i == currentPaletteIndex;
                    GUIStyle style = selected ? paletteSelectedStyle : paletteButtonStyle;
                    string prefix = selected ? "▶ " : "    ";
                    string hotkey = i < 9 ? $"[{i + 1}] " : "     ";
                    if (GUILayout.Button($"{prefix}{hotkey}{entry.tileId}", style))
                    {
                        currentPaletteIndex = i;
                        DestroyCursorPreview();
                    }
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);

            GUILayout.Label("文件", headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存", GUILayout.Height(26))) Save();
            if (GUILayout.Button("加载", GUILayout.Height(26))) Load();
            GUILayout.EndHorizontal();
            if (GUILayout.Button($"清空世界 {WorldLabel(activeWorld)}", GUILayout.Height(24))) ClearAllTiles();

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
                GUILayout.Label("数字键 1-9 · 选择地块", mutedStyle);
                GUILayout.EndVertical();
            }

            GUILayout.Space(4);
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label($"场景: {SceneManager.GetActiveScene().name}", mutedStyle);
            GUILayout.Label($"地图: {Path.GetFileName(SaveFilePath)}", mutedStyle);
            GUILayout.Label($"地块: A={placedTilesA.Count}  B={placedTilesB.Count}", mutedStyle);
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

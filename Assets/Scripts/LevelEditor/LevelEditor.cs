using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Gameplay;
using BokeGameJam.Input;

namespace BokeGameJam.LevelEditor
{
    /// <summary>
    /// 运行时关卡编辑器：完全事件驱动，不直接读 Unity Input。
    ///
    /// 依赖：
    ///   • InputManager 广播输入事件（M 切换、鼠标绘制、Ctrl+S/L/N、数字键选调色板等）
    ///   • CameraManager 处理相机跟随与编辑模式下的自由移动
    ///
    /// 交互：
    ///   M 键                — 切换 编辑 / 游玩 模式
    ///   编辑模式下：
    ///     WASD/方向键       — 移动相机（由 CameraManager 处理）
    ///     鼠标左键 / 右键   — 放置 / 删除地块
    ///   其余操作通过 GUI 面板按钮完成（保存/加载/清空/切换地块）。
    /// </summary>
    public sealed class LevelEditor : MonoBehaviour
    {
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
        [Tooltip("放置的地块会作为此 Transform 的子物体，方便清理")]
        [SerializeField] private Transform tilesRoot;

        [Tooltip("进入编辑模式时会禁用该玩家脚本，避免残留物理速度；留空则由 InputContext 自动屏蔽输入")]
        [SerializeField] private PlayerController playerToDisable;

        [Header("Editor State")]
        [Tooltip("勾选后进入场景直接是编辑模式；默认为游玩模式")]
        [SerializeField] private bool startInEditMode = false;
        [SerializeField] private bool showGridGizmos = true;

        [Header("Save / Load")]
        [Tooltip("保存文件名（放在 Application.persistentDataPath 下）")]
        [SerializeField] private string saveFileName = "level.json";

        private readonly Dictionary<Vector2Int, PlacedTile> placedTiles = new();
        private bool isEditMode;
        private Rect guiPanelRect = new(10, 10, 260, 360);
        private Vector2 paletteScroll;

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
        public string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (tilesRoot == null)
            {
                GameObject root = new("_TilesRoot");
                tilesRoot = root.transform;
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
        }

        private void Start()
        {
            SetEditMode(startInEditMode);
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

            // 切换输入上下文（若 InputManager 存在）
            if (InputManager.Instance != null)
                InputManager.Instance.SetContext(value ? InputContext.LevelEditor : InputContext.Gameplay);

            SetStatus(value ? "已进入编辑模式" : "已退出编辑模式");
        }

        private bool IsMouseOverGuiPanel()
        {
            Vector2 pos = UnityEngine.Input.mousePosition;
            // GUI 使用左上原点，Input.mousePosition 是左下原点 → 需翻转
            pos.y = Screen.height - pos.y;
            return guiPanelRect.Contains(pos);
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

            // 同格且同类型时跳过
            if (placedTiles.TryGetValue(cell, out PlacedTile existing))
            {
                if (existing.tileId == entry.tileId)
                    return;

                if (existing.instance != null)
                    Destroy(existing.instance);
            }

            GameObject instance = Instantiate(entry.prefab, CellToWorld(cell), Quaternion.identity, tilesRoot);
            instance.name = $"Tile_{entry.tileId}_{cell.x}_{cell.y}";
            placedTiles[cell] = new PlacedTile { tileId = entry.tileId, instance = instance };
        }

        public void RemoveTile(Vector2Int cell)
        {
            if (!placedTiles.TryGetValue(cell, out PlacedTile tile))
                return;

            if (tile.instance != null)
                Destroy(tile.instance);

            placedTiles.Remove(cell);
        }

        public void ClearAllTiles()
        {
            foreach (PlacedTile t in placedTiles.Values)
            {
                if (t.instance != null)
                    Destroy(t.instance);
            }

            placedTiles.Clear();
            SetStatus("已清空地图");
        }

        // ---------- 保存 / 加载 ----------

        public void Save()
        {
            LevelData data = new() { levelName = Path.GetFileNameWithoutExtension(saveFileName) };
            foreach (KeyValuePair<Vector2Int, PlacedTile> kv in placedTiles)
                data.tiles.Add(new LevelData.TileEntry(kv.Key.x, kv.Key.y, kv.Value.tileId));

            try
            {
                string path = SaveFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
                File.WriteAllText(path, LevelData.ToJson(data));
                SetStatus($"已保存 {data.tiles.Count} 块 -> {Path.GetFileName(path)}");
                Debug.Log($"[LevelEditor] 已保存 {data.tiles.Count} 个地块 -> {path}");
            }
            catch (System.Exception ex)
            {
                SetStatus($"保存失败: {ex.Message}");
                Debug.LogError($"[LevelEditor] 保存失败: {ex.Message}");
            }
        }

        public void Load()
        {
            string path = SaveFilePath;
            if (!File.Exists(path))
            {
                SetStatus($"未找到关卡文件: {Path.GetFileName(path)}");
                Debug.LogWarning($"[LevelEditor] 未找到关卡文件: {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                LevelData data = LevelData.FromJson(json);
                ApplyLevelData(data);
                SetStatus($"已加载 {data.tiles.Count} 块 <- {Path.GetFileName(path)}");
                Debug.Log($"[LevelEditor] 已加载 {data.tiles.Count} 个地块 <- {path}");
            }
            catch (System.Exception ex)
            {
                SetStatus($"加载失败: {ex.Message}");
                Debug.LogError($"[LevelEditor] 加载失败: {ex.Message}");
            }
        }

        /// <summary>把 LevelData 应用到场景（会先清空当前地块）。</summary>
        public void ApplyLevelData(LevelData data)
        {
            ClearAllTiles();
            if (data == null || data.tiles == null)
                return;

            int savedIndex = currentPaletteIndex;
            foreach (LevelData.TileEntry entry in data.tiles)
            {
                int idx = FindPaletteIndex(entry.tileId);
                if (idx < 0)
                {
                    Debug.LogWarning($"[LevelEditor] 找不到 tileId: {entry.tileId}，跳过");
                    continue;
                }

                currentPaletteIndex = idx;
                PlaceTile(new Vector2Int(entry.x, entry.y));
            }

            currentPaletteIndex = Mathf.Clamp(savedIndex, 0, Mathf.Max(0, tilePalette.Count - 1));
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

        // ---------- GUI ----------

        private void SetStatus(string message, float duration = 3f)
        {
            statusMessage = message;
            statusMessageExpireTime = Time.unscaledTime + duration;
        }

        private void OnGUI()
        {
            DrawModeBadge();

            if (!isEditMode)
                return;

            guiPanelRect = GUI.Window(9527, guiPanelRect, DrawEditorWindow, "关卡编辑器");
        }

        private void DrawModeBadge()
        {
            const float w = 220f;
            const float h = 28f;
            Rect r = new(Screen.width - w - 10f, 10f, w, h);
            GUI.Box(r, string.Empty);
            string text = isEditMode ? "编辑模式 (按 M 退出)" : "游玩模式 (按 M 进入编辑)";
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, r.height), text);
        }

        private void DrawEditorWindow(int id)
        {
            GUILayout.Space(4);

            if (!string.IsNullOrEmpty(statusMessage) && Time.unscaledTime < statusMessageExpireTime)
                GUILayout.Label($"● {statusMessage}");
            else
                GUILayout.Label(" ");

            GUILayout.Label("<b>地块面板</b>", RichLabelStyle());
            paletteScroll = GUILayout.BeginScrollView(paletteScroll, GUILayout.Height(110));
            for (int i = 0; i < tilePalette.Count; i++)
            {
                TilePaletteEntry entry = tilePalette[i];
                if (entry == null) continue;

                bool selected = i == currentPaletteIndex;
                string label = selected ? $"▶ {entry.tileId}" : $"   {entry.tileId}";
                if (GUILayout.Button(label))
                {
                    currentPaletteIndex = i;
                    DestroyCursorPreview();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("<b>操作</b>", RichLabelStyle());

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存")) Save();
            if (GUILayout.Button("加载")) Load();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("清空地图")) ClearAllTiles();

            GUILayout.Space(6);
            GUILayout.Label("<b>操作提示</b>", RichLabelStyle());
            GUILayout.Label("• 鼠标左键：放置    右键：删除");
            GUILayout.Label("• WASD / 方向键：移动相机");
            GUILayout.Label("• 按住 Shift：加速移动");
            GUILayout.Label($"文件: {SaveFilePath}");

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private static GUIStyle richLabelStyle;
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

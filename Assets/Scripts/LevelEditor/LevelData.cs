using System;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.LevelEditor
{
    /// <summary>
    /// 可序列化关卡数据，保存双世界地块的网格坐标及预制体标识。
    /// version 2：tilesA / tilesB；旧版 tiles 加载时迁入 tilesA。
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int version = 2;
        public string levelName = "Untitled";

        /// <summary>世界 A 地块。</summary>
        public List<TileEntry> tilesA = new();

        /// <summary>世界 B 地块。</summary>
        public List<TileEntry> tilesB = new();

        /// <summary>旧版单层字段，仅兼容读取；新存档不再写入。</summary>
        public List<TileEntry> tiles = new();

        [Serializable]
        public struct TileEntry
        {
            public int x;
            public int y;
            /// <summary>地块类型 id，对应 LevelEditor.tilePalette 中的 tileId。</summary>
            public string tileId;

            public TileEntry(int x, int y, string tileId)
            {
                this.x = x;
                this.y = y;
                this.tileId = tileId;
            }

            public Vector2Int Position => new(x, y);
        }

        public int TotalTileCount =>
            (tilesA?.Count ?? 0) + (tilesB?.Count ?? 0);

        public static string ToJson(LevelData data)
        {
            if (data != null)
            {
                data.version = 2;
                // 新存档不再写旧字段，避免双份数据
                data.tiles = new List<TileEntry>();
            }

            return JsonUtility.ToJson(data, prettyPrint: true);
        }

        public static LevelData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new LevelData();

            try
            {
                LevelData data = JsonUtility.FromJson<LevelData>(json);
                data ??= new LevelData();
                Normalize(data);
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelData] JSON 解析失败: {ex.Message}");
                return new LevelData();
            }
        }

        /// <summary>补全空列表，并把旧版 tiles 迁入 tilesA。</summary>
        public static void Normalize(LevelData data)
        {
            if (data == null)
                return;

            data.tilesA ??= new List<TileEntry>();
            data.tilesB ??= new List<TileEntry>();
            data.tiles ??= new List<TileEntry>();

            bool dualEmpty = data.tilesA.Count == 0 && data.tilesB.Count == 0;
            if (dualEmpty && data.tiles.Count > 0)
            {
                data.tilesA.AddRange(data.tiles);
                data.tiles.Clear();
                data.version = 2;
            }
        }
    }
}

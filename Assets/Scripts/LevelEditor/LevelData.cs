using System;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.LevelEditor
{
    /// <summary>
    /// 可序列化关卡数据，保存所有地块的网格坐标及预制体标识。
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int version = 1;
        public string levelName = "Untitled";
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

        public static string ToJson(LevelData data)
        {
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
                data.tiles ??= new List<TileEntry>();
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelData] JSON 解析失败: {ex.Message}");
                return new LevelData();
            }
        }
    }
}

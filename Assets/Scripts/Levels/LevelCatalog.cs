using System;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Levels
{
    /// <summary>
    /// 关卡目录（ScriptableObject）。所有关卡"一处配置，处处可用"。
    ///
    /// 使用步骤：
    ///   1. 项目面板右键 → Create → BokeGameJam → Level Catalog，得到一个资源文件
    ///   2. 展开 Levels 列表，每条填写 LevelId / DisplayName / SceneName / LevelFile / Thumbnail
    ///   3. 将该资源拖到 LevelSelectController.Catalog 字段（或放到 Resources/Levels/ 下按名称加载）
    ///
    /// 关卡进度会以 <see cref="progressKeyPrefix"/> + levelId 存到 PlayerPrefs。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "BokeGameJam/Level Catalog", order = 100)]
    public sealed class LevelCatalog : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            [Tooltip("关卡唯一 id，例如 level_1、boss。留空会自动使用 level_索引 作为回退")]
            public string levelId;

            [Tooltip("按钮上显示的名字；留空使用 levelId")]
            public string displayName;

            [Tooltip("要加载的场景名（与 Build Settings 里的场景名一致）")]
            public string sceneName;

            [Tooltip("LevelEditor 保存/加载用的 JSON 文件名，可为空")]
            public string levelFile;

            [Tooltip("按钮缩略图，可为空")]
            public Sprite thumbnail;

            [Tooltip("默认是否解锁；未解锁的关卡按钮 interactable=false")]
            public bool unlockedByDefault = true;
        }

        [Header("Data")]
        [SerializeField] private List<Level> levels = new();

        [Header("Progress")]
        [Tooltip("是否使用 PlayerPrefs 持久化解锁状态")]
        [SerializeField] private bool useProgressPersistence = true;

        [SerializeField] private string progressKeyPrefix = "LevelUnlocked_";

        public IReadOnlyList<Level> Levels => levels;
        public int Count => levels.Count;

        // ---------- 查询 ----------

        public Level Get(int index)
        {
            return index >= 0 && index < levels.Count ? levels[index] : null;
        }

        public Level Get(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) return null;
            foreach (Level l in levels)
            {
                if (l != null && l.levelId == levelId)
                    return l;
            }
            return null;
        }

        public int IndexOf(string levelId)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null && levels[i].levelId == levelId)
                    return i;
            }
            return -1;
        }

        public string ResolveLevelId(int index)
        {
            Level l = Get(index);
            if (l != null && !string.IsNullOrWhiteSpace(l.levelId))
                return l.levelId.Trim();
            return $"level_{index + 1}";
        }

        // ---------- 解锁进度 ----------

        public bool IsUnlocked(int index)
        {
            Level l = Get(index);
            return l != null && IsUnlocked(l);
        }

        public bool IsUnlocked(Level level)
        {
            if (level == null) return false;
            if (!useProgressPersistence) return level.unlockedByDefault;

            string key = progressKeyPrefix + level.levelId;
            int stored = PlayerPrefs.GetInt(key, level.unlockedByDefault ? 1 : 0);
            return stored != 0;
        }

        public void SetUnlocked(string levelId, bool unlocked)
        {
            Level l = Get(levelId);
            if (l == null) return;

            if (useProgressPersistence)
            {
                PlayerPrefs.SetInt(progressKeyPrefix + l.levelId, unlocked ? 1 : 0);
                PlayerPrefs.Save();
            }
            else
            {
                l.unlockedByDefault = unlocked;
            }
        }

        public void ResetProgress()
        {
            if (!useProgressPersistence) return;
            foreach (Level l in levels)
            {
                if (l == null) continue;
                PlayerPrefs.DeleteKey(progressKeyPrefix + l.levelId);
            }
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 自动补 levelId：便于 Inspector 拖出一堆空条目后快速填
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null && string.IsNullOrWhiteSpace(levels[i].levelId))
                    levels[i].levelId = $"level_{i + 1}";
            }
        }
#endif
    }
}

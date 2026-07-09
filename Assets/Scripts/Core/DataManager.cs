using System;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    /// <summary>
    /// 全局 PlayerPrefs 数据管理器（单例，跨场景不销毁）。
    /// 通过 Inspector 的注册表维护项目中使用的 prefs 键，便于查阅与统一读写。
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance { get; private set; }

        /// <summary>
        /// 常用 PlayerPrefs 键 id 常量。新增存档字段时请先在此声明，再登记到 registeredPrefs。
        /// </summary>
        public static class Keys
        {
            /// <summary>BGM 与 SFX 共用的主音量（0-1）。</summary>
            public const string MasterVolume = "MasterVolume";

            /// <summary>BGM 音量（预留，当前主流程使用 MasterVolume）。</summary>
            public const string BgmVolume = "BgmVolume";

            /// <summary>SFX 音量（预留，当前主流程使用 MasterVolume）。</summary>
            public const string SfxVolume = "SfxVolume";
        }

        /// <summary>PlayerPrefs 值类型。</summary>
        public enum PrefValueType
        {
            Int,
            Float,
            String
        }

        /// <summary>
        /// 单条已注册的 PlayerPrefs 条目（逻辑 id、真实键名、类型与默认值）。
        /// </summary>
        [Serializable]
        public sealed class PrefEntry
        {
            [Tooltip("代码使用的逻辑 id（优先使用 DataManager.Keys 常量）。")]
            [SerializeField] private string id;

            [Tooltip("实际写入 PlayerPrefs 的键名。留空则使用 id。")]
            [SerializeField] private string key;

            [Tooltip("该键对应的值类型。")]
            [SerializeField] private PrefValueType valueType = PrefValueType.Int;

            [TextArea(1, 3)]
            [Tooltip("用途说明，仅用于查阅。")]
            [SerializeField] private string description;

            [SerializeField] private int defaultInt;
            [SerializeField] private float defaultFloat;
            [SerializeField] private string defaultString = string.Empty;

            public PrefEntry()
            {
            }

            public PrefEntry(
                string id,
                PrefValueType valueType,
                string description = null,
                string key = null,
                int defaultInt = 0,
                float defaultFloat = 0f,
                string defaultString = "")
            {
                this.id = id;
                this.key = key;
                this.valueType = valueType;
                this.description = description;
                this.defaultInt = defaultInt;
                this.defaultFloat = defaultFloat;
                this.defaultString = defaultString ?? string.Empty;
            }

            /// <summary>逻辑 id（去空白后）。</summary>
            public string Id => string.IsNullOrWhiteSpace(id) ? null : id.Trim();

            /// <summary>真实 PlayerPrefs 键名；未单独填写时回退为 Id。</summary>
            public string Key
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(key))
                        return key.Trim();

                    return Id;
                }
            }

            public PrefValueType ValueType => valueType;
            public string Description => description;
            public int DefaultInt => defaultInt;
            public float DefaultFloat => defaultFloat;
            public string DefaultString => defaultString ?? string.Empty;

            /// <summary>更新本条目的全部字段（用于运行时 Register）。</summary>
            public void Apply(
                string newId,
                PrefValueType newValueType,
                string newDescription,
                string newKey,
                int newDefaultInt,
                float newDefaultFloat,
                string newDefaultString)
            {
                id = newId;
                valueType = newValueType;
                description = newDescription;
                key = newKey;
                defaultInt = newDefaultInt;
                defaultFloat = newDefaultFloat;
                defaultString = newDefaultString ?? string.Empty;
            }
        }

        [Header("已注册的 PlayerPrefs")]
        [Tooltip("在此登记项目中使用的所有 PlayerPrefs 键，便于统一管理。")]
        [SerializeField] private List<PrefEntry> registeredPrefs = new();

        [Header("选项")]
        [Tooltip("访问未注册键时是否输出警告（仍会按原始字符串读写）。")]
        [SerializeField] private bool warnOnUnregisteredKey = true;

        [Tooltip("每次写入后是否自动调用 PlayerPrefs.Save。")]
        [SerializeField] private bool autoSave = true;

        private readonly Dictionary<string, PrefEntry> entriesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PrefEntry> entriesByKey = new(StringComparer.Ordinal);

        /// <summary>当前已登记的 prefs 列表（只读）。</summary>
        public IReadOnlyList<PrefEntry> RegisteredPrefs => registeredPrefs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            RebuildLookup();
        }

        private void OnValidate()
        {
            // Inspector 修改列表后同步查找表
            RebuildLookup();
        }

        #region 注册

        /// <summary>
        /// 根据 Inspector 列表重建 id / 键名查找表。
        /// </summary>
        public void RebuildLookup()
        {
            entriesById.Clear();
            entriesByKey.Clear();

            if (registeredPrefs == null)
                return;

            for (int i = 0; i < registeredPrefs.Count; i++)
            {
                PrefEntry entry = registeredPrefs[i];
                if (entry == null)
                    continue;

                string id = entry.Id;
                string key = entry.Key;
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning($"[DataManager] Pref entry at index {i} has empty id/key.", this);
                    continue;
                }

                if (entriesById.ContainsKey(id))
                    Debug.LogWarning($"[DataManager] Duplicate pref id: {id}", this);
                else
                    entriesById[id] = entry;

                if (entriesByKey.ContainsKey(key))
                    Debug.LogWarning($"[DataManager] Duplicate pref key: {key}", this);
                else
                    entriesByKey[key] = entry;
            }
        }

        /// <summary>
        /// 运行时注册或更新一条 pref，并写入 registeredPrefs 列表。
        /// </summary>
        public PrefEntry Register(
            string id,
            PrefValueType valueType,
            string description = null,
            string key = null,
            int defaultInt = 0,
            float defaultFloat = 0f,
            string defaultString = "")
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("[DataManager] Cannot register pref with empty id.", this);
                return null;
            }

            string normalizedId = id.Trim();
            string normalizedKey = string.IsNullOrWhiteSpace(key) ? normalizedId : key.Trim();

            PrefEntry entry = FindEntryById(normalizedId);
            if (entry == null)
            {
                entry = new PrefEntry(
                    normalizedId,
                    valueType,
                    description,
                    normalizedKey,
                    defaultInt,
                    defaultFloat,
                    defaultString);
                registeredPrefs.Add(entry);
            }
            else
            {
                entry.Apply(
                    normalizedId,
                    valueType,
                    description,
                    normalizedKey,
                    defaultInt,
                    defaultFloat,
                    defaultString);
            }

            RebuildLookup();
            return entry;
        }

        /// <summary>按逻辑 id 或真实键名查找注册条目。</summary>
        public bool TryGetEntry(string idOrKey, out PrefEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(idOrKey))
                return false;

            string normalized = idOrKey.Trim();
            if (entriesById.TryGetValue(normalized, out entry))
                return true;

            return entriesByKey.TryGetValue(normalized, out entry);
        }

        /// <summary>判断指定 id/键是否已登记。</summary>
        public bool IsRegistered(string idOrKey)
        {
            return TryGetEntry(idOrKey, out _);
        }

        /// <summary>返回所有已登记的逻辑 id。</summary>
        public IEnumerable<string> GetRegisteredIds()
        {
            return entriesById.Keys;
        }

        /// <summary>返回所有已登记的真实 PlayerPrefs 键名。</summary>
        public IEnumerable<string> GetRegisteredKeys()
        {
            return entriesByKey.Keys;
        }

        #endregion

        #region 读写

        /// <summary>PlayerPrefs 中是否已存在该键的值。</summary>
        public bool HasKey(string idOrKey)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return false;

            return PlayerPrefs.HasKey(key);
        }

        /// <summary>读取 int；未传默认值时使用注册表中的默认值。</summary>
        public int GetInt(string idOrKey, int? defaultValue = null)
        {
            if (!TryResolveKey(idOrKey, out string key, out PrefEntry entry))
                return defaultValue ?? 0;

            int fallback = defaultValue ?? entry?.DefaultInt ?? 0;
            return PlayerPrefs.GetInt(key, fallback);
        }

        /// <summary>写入 int。</summary>
        public void SetInt(string idOrKey, int value)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return;

            PlayerPrefs.SetInt(key, value);
            MaybeSave();
        }

        /// <summary>读取 float；未传默认值时使用注册表中的默认值。</summary>
        public float GetFloat(string idOrKey, float? defaultValue = null)
        {
            if (!TryResolveKey(idOrKey, out string key, out PrefEntry entry))
                return defaultValue ?? 0f;

            float fallback = defaultValue ?? entry?.DefaultFloat ?? 0f;
            return PlayerPrefs.GetFloat(key, fallback);
        }

        /// <summary>写入 float。</summary>
        public void SetFloat(string idOrKey, float value)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return;

            PlayerPrefs.SetFloat(key, value);
            MaybeSave();
        }

        /// <summary>读取 string；未传默认值时使用注册表中的默认值。</summary>
        public string GetString(string idOrKey, string defaultValue = null)
        {
            if (!TryResolveKey(idOrKey, out string key, out PrefEntry entry))
                return defaultValue ?? string.Empty;

            string fallback = defaultValue ?? entry?.DefaultString ?? string.Empty;
            return PlayerPrefs.GetString(key, fallback);
        }

        /// <summary>写入 string。</summary>
        public void SetString(string idOrKey, string value)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return;

            PlayerPrefs.SetString(key, value ?? string.Empty);
            MaybeSave();
        }

        /// <summary>以 0/1 int 形式读取布尔值。</summary>
        public bool GetBool(string idOrKey, bool defaultValue = false)
        {
            return GetInt(idOrKey, defaultValue ? 1 : 0) != 0;
        }

        /// <summary>以 0/1 int 形式写入布尔值。</summary>
        public void SetBool(string idOrKey, bool value)
        {
            SetInt(idOrKey, value ? 1 : 0);
        }

        /// <summary>删除单个键。</summary>
        public void DeleteKey(string idOrKey)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return;

            PlayerPrefs.DeleteKey(key);
            MaybeSave();
        }

        /// <summary>
        /// 仅删除已注册表中的键（推荐用于「清空游戏数据」）。
        /// </summary>
        public void DeleteAllRegistered()
        {
            foreach (PrefEntry entry in entriesByKey.Values)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                    continue;

                PlayerPrefs.DeleteKey(entry.Key);
            }

            MaybeSave();
        }

        /// <summary>
        /// 删除本设备上全部 PlayerPrefs。游戏内清理请优先使用 <see cref="DeleteAllRegistered"/>。
        /// </summary>
        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            MaybeSave();
        }

        /// <summary>立即将 PlayerPrefs 刷盘。</summary>
        public void Save()
        {
            PlayerPrefs.Save();
        }

        #endregion

        #region 内部

        /// <summary>在列表中按逻辑 id 查找条目。</summary>
        private PrefEntry FindEntryById(string id)
        {
            if (entriesById.TryGetValue(id, out PrefEntry entry))
                return entry;

            if (registeredPrefs == null)
                return null;

            for (int i = 0; i < registeredPrefs.Count; i++)
            {
                PrefEntry item = registeredPrefs[i];
                if (item != null && string.Equals(item.Id, id, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private bool TryResolveKey(string idOrKey, out string key)
        {
            return TryResolveKey(idOrKey, out key, out _);
        }

        /// <summary>
        /// 将逻辑 id 或键名解析为真实 PlayerPrefs 键。
        /// 未注册时可选警告，并直接把输入当作原始键使用。
        /// </summary>
        private bool TryResolveKey(string idOrKey, out string key, out PrefEntry entry)
        {
            key = null;
            entry = null;

            if (string.IsNullOrWhiteSpace(idOrKey))
            {
                Debug.LogWarning("[DataManager] Pref id/key is empty.", this);
                return false;
            }

            string normalized = idOrKey.Trim();
            if (TryGetEntry(normalized, out entry))
            {
                key = entry.Key;
                return !string.IsNullOrEmpty(key);
            }

            if (warnOnUnregisteredKey)
                Debug.LogWarning($"[DataManager] Unregistered pref '{normalized}'. Using it as a raw PlayerPrefs key.", this);

            key = normalized;
            return true;
        }

        /// <summary>按 autoSave 选项决定是否立即 Save。</summary>
        private void MaybeSave()
        {
            if (autoSave)
                PlayerPrefs.Save();
        }

        #endregion
    }
}

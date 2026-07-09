using System;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    /// <summary>
    /// Global PlayerPrefs manager. Registered keys live in this script / Inspector list
    /// so the project can see which prefs exist.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance { get; private set; }

        /// <summary>
        /// Well-known PlayerPrefs key ids. Add new constants here when introducing prefs.
        /// </summary>
        public static class Keys
        {
            /// <summary>Shared master volume for BGM and SFX (0-1).</summary>
            public const string MasterVolume = "MasterVolume";

            public const string BgmVolume = "BgmVolume";
            public const string SfxVolume = "SfxVolume";
        }

        public enum PrefValueType
        {
            Int,
            Float,
            String
        }

        [Serializable]
        public sealed class PrefEntry
        {
            [Tooltip("Logical id used by code (prefer DataManager.Keys constants).")]
            [SerializeField] private string id;

            [Tooltip("Actual PlayerPrefs key. Leave empty to use id.")]
            [SerializeField] private string key;

            [SerializeField] private PrefValueType valueType = PrefValueType.Int;

            [TextArea(1, 3)]
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

            public string Id => string.IsNullOrWhiteSpace(id) ? null : id.Trim();

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

        [Header("Registered PlayerPrefs")]
        [Tooltip("Register every PlayerPrefs key used by the project here.")]
        [SerializeField] private List<PrefEntry> registeredPrefs = new();

        [Header("Options")]
        [SerializeField] private bool warnOnUnregisteredKey = true;
        [SerializeField] private bool autoSave = true;

        private readonly Dictionary<string, PrefEntry> entriesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PrefEntry> entriesByKey = new(StringComparer.Ordinal);

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
            RebuildLookup();
        }

        #region Registration

        /// <summary>
        /// Rebuilds id/key lookup from the Inspector list.
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
        /// Registers or updates a pref entry at runtime and keeps it in the list.
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

        public bool IsRegistered(string idOrKey)
        {
            return TryGetEntry(idOrKey, out _);
        }

        public IEnumerable<string> GetRegisteredIds()
        {
            return entriesById.Keys;
        }

        public IEnumerable<string> GetRegisteredKeys()
        {
            return entriesByKey.Keys;
        }

        #endregion

        #region Read / Write

        public bool HasKey(string idOrKey)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return false;

            return PlayerPrefs.HasKey(key);
        }

        public int GetInt(string idOrKey, int? defaultValue = null)
        {
            if (!TryResolveKey(idOrKey, out string key, out PrefEntry entry))
                return defaultValue ?? 0;

            int fallback = defaultValue ?? entry?.DefaultInt ?? 0;
            return PlayerPrefs.GetInt(key, fallback);
        }

        public void SetInt(string idOrKey, int value)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return;

            PlayerPrefs.SetInt(key, value);
            MaybeSave();
        }

        public float GetFloat(string idOrKey, float? defaultValue = null)
        {
            if (!TryResolveKey(idOrKey, out string key, out PrefEntry entry))
                return defaultValue ?? 0f;

            float fallback = defaultValue ?? entry?.DefaultFloat ?? 0f;
            return PlayerPrefs.GetFloat(key, fallback);
        }

        public void SetFloat(string idOrKey, float value)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return;

            PlayerPrefs.SetFloat(key, value);
            MaybeSave();
        }

        public string GetString(string idOrKey, string defaultValue = null)
        {
            if (!TryResolveKey(idOrKey, out string key, out PrefEntry entry))
                return defaultValue ?? string.Empty;

            string fallback = defaultValue ?? entry?.DefaultString ?? string.Empty;
            return PlayerPrefs.GetString(key, fallback);
        }

        public void SetString(string idOrKey, string value)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return;

            PlayerPrefs.SetString(key, value ?? string.Empty);
            MaybeSave();
        }

        public bool GetBool(string idOrKey, bool defaultValue = false)
        {
            return GetInt(idOrKey, defaultValue ? 1 : 0) != 0;
        }

        public void SetBool(string idOrKey, bool value)
        {
            SetInt(idOrKey, value ? 1 : 0);
        }

        public void DeleteKey(string idOrKey)
        {
            if (!TryResolveKey(idOrKey, out string key))
                return;

            PlayerPrefs.DeleteKey(key);
            MaybeSave();
        }

        /// <summary>
        /// Deletes only keys listed in the registered prefs table.
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
        /// Deletes every PlayerPrefs key on this device. Prefer DeleteAllRegistered for game data.
        /// </summary>
        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            MaybeSave();
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }

        #endregion

        #region Internal

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

        private void MaybeSave()
        {
            if (autoSave)
                PlayerPrefs.Save();
        }

        #endregion
    }
}

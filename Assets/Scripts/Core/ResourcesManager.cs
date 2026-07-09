using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    /// <summary>
    /// Bottom-level access point for resources defined in ResourceDefinitionDatabase.
    /// </summary>
    public static class ResourcesManager
    {
        private const string DatabaseResourcePath = "ScriptableObjects/ResourceDefinitionDatabase";

        private static readonly Dictionary<string, GameObject> uiPrefabs = new();
        private static readonly Dictionary<string, GameObject> prefabs = new();
        private static readonly Dictionary<string, Sprite> sprites = new();
        private static readonly Dictionary<string, AudioClip> sounds = new();

        private static ResourceDefinitionDatabase database;

        public static ResourceDefinitionDatabase Database
        {
            get
            {
                EnsureDatabase();
                return database;
            }
        }

        public static void SetDatabase(ResourceDefinitionDatabase resourceDatabase)
        {
            database = resourceDatabase;
            ClearAll();
        }

        public static bool TryGetUI(string id, out ResourceDefinitionDatabase.UIResource resource)
        {
            resource = null;
            return EnsureDatabase() && database.TryGetUI(id, out resource);
        }

        public static bool TryGetPrefab(string id, out ResourceDefinitionDatabase.PrefabResource resource)
        {
            resource = null;
            return EnsureDatabase() && database.TryGetPrefab(id, out resource);
        }

        public static bool TryGetSprite(string id, out ResourceDefinitionDatabase.SpriteResource resource)
        {
            resource = null;
            return EnsureDatabase() && database.TryGetSprite(id, out resource);
        }

        public static bool TryGetSound(string id, out ResourceDefinitionDatabase.SoundResource resource)
        {
            resource = null;
            return EnsureDatabase() && database.TryGetSound(id, out resource);
        }

        public static bool TryGetScene(string id, out ResourceDefinitionDatabase.SceneResource resource)
        {
            resource = null;
            return EnsureDatabase() && database.TryGetScene(id, out resource);
        }

        public static GameObject LoadUI(ResourceDefinitionDatabase.UIResource resource)
        {
            resource = Database?.ResolveUI(resource);
            if (resource == null)
            {
                Debug.LogWarning("[ResourcesManager] UI resource is null.");
                return null;
            }

            return LoadAsset(resource.Id, resource.Prefab, uiPrefabs, "UI prefab");
        }

        public static GameObject LoadPrefab(ResourceDefinitionDatabase.PrefabResource resource)
        {
            resource = Database?.ResolvePrefab(resource);
            if (resource == null)
            {
                Debug.LogWarning("[ResourcesManager] Prefab resource is null.");
                return null;
            }

            return LoadAsset(resource.Id, resource.Prefab, prefabs, "Prefab");
        }

        public static GameObject LoadUIById(string id)
        {
            if (!TryGetUI(id, out ResourceDefinitionDatabase.UIResource resource))
            {
                Debug.LogError($"[ResourcesManager] Cannot find UI resource id: {id}");
                return null;
            }

            return LoadUI(resource);
        }

        public static GameObject LoadPrefabById(string id)
        {
            if (!TryGetPrefab(id, out ResourceDefinitionDatabase.PrefabResource resource))
            {
                Debug.LogError($"[ResourcesManager] Cannot find prefab resource id: {id}");
                return null;
            }

            return LoadPrefab(resource);
        }

        public static GameObject SpawnUI(ResourceDefinitionDatabase.UIResource resource, Transform parent = null)
        {
            GameObject prefab = LoadUI(resource);
            return prefab != null ? Object.Instantiate(prefab, parent) : null;
        }

        public static GameObject SpawnPrefab(ResourceDefinitionDatabase.PrefabResource resource, Transform parent = null)
        {
            GameObject prefab = LoadPrefab(resource);
            return prefab != null ? Object.Instantiate(prefab, parent) : null;
        }

        public static GameObject SpawnUIById(string id, Transform parent = null)
        {
            GameObject prefab = LoadUIById(id);
            return prefab != null ? Object.Instantiate(prefab, parent) : null;
        }

        public static GameObject SpawnPrefabById(string id, Transform parent = null)
        {
            GameObject prefab = LoadPrefabById(id);
            return prefab != null ? Object.Instantiate(prefab, parent) : null;
        }

        public static Sprite LoadSprite(ResourceDefinitionDatabase.SpriteResource resource)
        {
            resource = Database?.ResolveSprite(resource);
            if (resource == null)
            {
                Debug.LogWarning("[ResourcesManager] Sprite resource is null.");
                return null;
            }

            return LoadAsset(resource.Id, resource.Sprite, sprites, "Sprite");
        }

        public static Sprite LoadSpriteById(string id)
        {
            if (!TryGetSprite(id, out ResourceDefinitionDatabase.SpriteResource resource))
            {
                Debug.LogError($"[ResourcesManager] Cannot find sprite resource id: {id}");
                return null;
            }

            return LoadSprite(resource);
        }

        public static AudioClip LoadSound(ResourceDefinitionDatabase.SoundResource resource)
        {
            resource = Database?.ResolveSound(resource);
            if (resource == null)
            {
                Debug.LogWarning("[ResourcesManager] Sound resource is null.");
                return null;
            }

            return LoadAsset(resource.Id, resource.Clip, sounds, "Sound");
        }

        public static AudioClip LoadSoundById(string id)
        {
            if (!TryGetSound(id, out ResourceDefinitionDatabase.SoundResource resource))
            {
                Debug.LogError($"[ResourcesManager] Cannot find sound resource id: {id}");
                return null;
            }

            return LoadSound(resource);
        }

        public static string GetSceneName(ResourceDefinitionDatabase.SceneResource resource)
        {
            resource = Database?.ResolveScene(resource);
            if (resource == null)
            {
                Debug.LogWarning("[ResourcesManager] Scene resource is null.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(resource.SceneName))
            {
                Debug.LogError($"[ResourcesManager] Scene resource '{resource.Id}' has no scene name.");
                return null;
            }

            return resource.SceneName.Trim();
        }

        public static string GetSceneNameById(string id)
        {
            if (!TryGetScene(id, out ResourceDefinitionDatabase.SceneResource resource))
            {
                Debug.LogError($"[ResourcesManager] Cannot find scene resource id: {id}");
                return null;
            }

            return GetSceneName(resource);
        }

        public static void ClearUI(ResourceDefinitionDatabase.UIResource resource)
        {
            ClearCache(resource?.Id, uiPrefabs);
        }

        public static void ClearPrefab(ResourceDefinitionDatabase.PrefabResource resource)
        {
            ClearCache(resource?.Id, prefabs);
        }

        public static void ClearSprite(ResourceDefinitionDatabase.SpriteResource resource)
        {
            ClearCache(resource?.Id, sprites);
        }

        public static void ClearSound(ResourceDefinitionDatabase.SoundResource resource)
        {
            ClearCache(resource?.Id, sounds);
        }

        public static void ClearAll()
        {
            uiPrefabs.Clear();
            prefabs.Clear();
            sprites.Clear();
            sounds.Clear();
        }

        private static T LoadAsset<T>(string id, T asset, Dictionary<string, T> cache, string resourceType)
            where T : Object
        {
            string normalizedId = NormalizeId(id);
            if (string.IsNullOrEmpty(normalizedId))
            {
                Debug.LogWarning($"[ResourcesManager] {resourceType} id is empty.");
                return null;
            }

            if (cache.TryGetValue(normalizedId, out T cached) && cached != null)
                return cached;

            if (asset == null)
            {
                Debug.LogError($"[ResourcesManager] {resourceType} '{normalizedId}' has no asset reference.");
                return null;
            }

            cache[normalizedId] = asset;
            return asset;
        }

        private static void ClearCache<T>(string id, Dictionary<string, T> cache)
            where T : Object
        {
            string normalizedId = NormalizeId(id);
            if (string.IsNullOrEmpty(normalizedId))
                return;

            cache.Remove(normalizedId);
        }

        private static bool EnsureDatabase()
        {
            if (database != null)
                return true;

            database = Resources.Load<ResourceDefinitionDatabase>(DatabaseResourcePath);
            if (database != null)
                return true;

            Debug.LogError($"[ResourcesManager] Cannot load resource database at Resources/{DatabaseResourcePath}.");
            return false;
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }
    }
}

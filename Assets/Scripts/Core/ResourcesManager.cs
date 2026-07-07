using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    /// <summary>
    /// Lightweight global Resources loader for common UI, sprite, and sound assets.
    /// Usage: ResourcesManager.LoadUI("Panel"), ResourcesManager.LoadSprite("Icon"), ResourcesManager.LoadSound("Click").
    /// </summary>
    public static class ResourcesManager
    {
        private const string UIPrefabResourcePath = "Prefabs/UI/";
        private const string SpriteResourcePath = "Art/Pictures/";
        private const string SoundResourcePath = "Audio/SFX/";
        private const string MusicResourcePath = "Audio/Music/";

        private static readonly Dictionary<string, GameObject> uiPrefabs = new();
        private static readonly Dictionary<string, Sprite> sprites = new();
        private static readonly Dictionary<string, AudioClip> sounds = new();
        private static readonly Dictionary<string, AudioClip> music = new();

        public static GameObject LoadUI(string prefabName)
        {
            return LoadResource(UIPrefabResourcePath, prefabName, uiPrefabs, "UI prefab");
        }

        public static GameObject LoadUIAtPath(string resourcePath)
        {
            return LoadResourceAtPath(resourcePath, uiPrefabs, "UI prefab");
        }

        public static GameObject SpawnUI(string prefabName, Transform parent = null)
        {
            GameObject prefab = LoadUI(prefabName);
            if (prefab == null)
                return null;

            return Object.Instantiate(prefab, parent);
        }

        public static GameObject SpawnUIAtPath(string resourcePath, Transform parent = null)
        {
            GameObject prefab = LoadUIAtPath(resourcePath);
            if (prefab == null)
                return null;

            return Object.Instantiate(prefab, parent);
        }

        public static Sprite LoadSprite(string spriteName)
        {
            return LoadResource(SpriteResourcePath, spriteName, sprites, "Sprite");
        }

        public static Sprite LoadSpriteAtPath(string resourcePath)
        {
            return LoadResourceAtPath(resourcePath, sprites, "Sprite");
        }

        public static AudioClip LoadSound(string soundName)
        {
            return LoadResource(SoundResourcePath, soundName, sounds, "Sound");
        }

        public static AudioClip LoadSoundAtPath(string resourcePath)
        {
            return LoadResourceAtPath(resourcePath, sounds, "Sound");
        }

        public static AudioClip LoadMusic(string musicName)
        {
            return LoadResource(MusicResourcePath, musicName, music, "Music");
        }

        public static AudioClip LoadMusicAtPath(string resourcePath)
        {
            return LoadResourceAtPath(resourcePath, music, "Music");
        }

        public static void ClearUI(string prefabName)
        {
            ClearCache(UIPrefabResourcePath, prefabName, uiPrefabs);
        }

        public static void ClearUIAtPath(string resourcePath)
        {
            ClearCacheAtPath(resourcePath, uiPrefabs);
        }

        public static void ClearSprite(string spriteName)
        {
            ClearCache(SpriteResourcePath, spriteName, sprites);
        }

        public static void ClearSpriteAtPath(string resourcePath)
        {
            ClearCacheAtPath(resourcePath, sprites);
        }

        public static void ClearSound(string soundName)
        {
            ClearCache(SoundResourcePath, soundName, sounds);
        }

        public static void ClearSoundAtPath(string resourcePath)
        {
            ClearCacheAtPath(resourcePath, sounds);
        }

        public static void ClearMusic(string musicName)
        {
            ClearCache(MusicResourcePath, musicName, music);
        }

        public static void ClearMusicAtPath(string resourcePath)
        {
            ClearCacheAtPath(resourcePath, music);
        }

        public static void ClearAll()
        {
            uiPrefabs.Clear();
            sprites.Clear();
            sounds.Clear();
            music.Clear();
        }

        private static T LoadResource<T>(string resourcePath, string resourceName, Dictionary<string, T> cache, string resourceType)
            where T : Object
        {
            string fullPath = BuildResourcePath(resourcePath, resourceName, resourceType);
            if (string.IsNullOrEmpty(fullPath))
                return null;

            return LoadResourceAtPath(fullPath, cache, resourceType);
        }

        private static T LoadResourceAtPath<T>(string resourcePath, Dictionary<string, T> cache, string resourceType)
            where T : Object
        {
            string normalizedPath = NormalizePath(resourcePath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                Debug.LogWarning($"[ResourcesManager] {resourceType} path is empty.");
                return null;
            }

            if (cache.TryGetValue(normalizedPath, out T cached) && cached != null)
                return cached;

            T resource = Resources.Load<T>(normalizedPath);
            if (resource == null)
            {
                Debug.LogError($"[ResourcesManager] Cannot find {resourceType}: {normalizedPath}");
                return null;
            }

            cache[normalizedPath] = resource;
            return resource;
        }

        private static void ClearCache<T>(string resourcePath, string resourceName, Dictionary<string, T> cache)
            where T : Object
        {
            string fullPath = BuildResourcePath(resourcePath, resourceName, "Resource");
            if (string.IsNullOrEmpty(fullPath))
                return;

            ClearCacheAtPath(fullPath, cache);
        }

        private static void ClearCacheAtPath<T>(string resourcePath, Dictionary<string, T> cache)
            where T : Object
        {
            string normalizedPath = NormalizePath(resourcePath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                Debug.LogWarning("[ResourcesManager] Resource path is empty.");
                return;
            }

            cache.Remove(normalizedPath);
        }

        private static string BuildResourcePath(string resourcePath, string resourceName, string resourceType)
        {
            string normalizedName = NormalizePath(resourceName);
            if (string.IsNullOrEmpty(normalizedName))
            {
                Debug.LogWarning($"[ResourcesManager] {resourceType} name is empty.");
                return null;
            }

            string normalizedRoot = NormalizePath(resourcePath);
            if (string.IsNullOrEmpty(normalizedRoot))
                return normalizedName;

            if (normalizedName.StartsWith(normalizedRoot + "/"))
                return normalizedName;

            return $"{normalizedRoot}/{normalizedName}";
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? null
                : path.Trim().Replace('\\', '/').Trim('/');
        }
    }
}

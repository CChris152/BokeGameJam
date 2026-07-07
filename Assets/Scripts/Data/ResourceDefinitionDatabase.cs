using System;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    [CreateAssetMenu(fileName = "ResourceDefinitionDatabase", menuName = "BokeGameJam/Resource Definition Database")]
    public class ResourceDefinitionDatabase : ScriptableObject
    {
        [SerializeField] private List<UIResource> uiPrefabs = new();
        [SerializeField] private List<SpriteResource> sprites = new();
        [SerializeField] private List<SoundResource> sounds = new();
        [SerializeField] private List<SceneResource> scenes = new();

        public IReadOnlyList<UIResource> UIPrefabs => uiPrefabs;
        public IReadOnlyList<SpriteResource> Sprites => sprites;
        public IReadOnlyList<SoundResource> Sounds => sounds;
        public IReadOnlyList<SceneResource> Scenes => scenes;

        public bool TryGetUI(string id, out UIResource resource)
        {
            return TryGetByIdOrFirst(uiPrefabs, id, out resource);
        }

        public bool TryGetSprite(string id, out SpriteResource resource)
        {
            return TryGetByIdOrFirst(sprites, id, out resource);
        }

        public bool TryGetSound(string id, out SoundResource resource)
        {
            return TryGetByIdOrFirst(sounds, id, out resource);
        }

        public bool TryGetScene(string id, out SceneResource resource)
        {
            return TryGetByIdOrFirst(scenes, id, out resource);
        }

        public UIResource ResolveUI(UIResource resource)
        {
            return ResolveResource(resource, uiPrefabs, IsUIConfigured);
        }

        public SpriteResource ResolveSprite(SpriteResource resource)
        {
            return ResolveResource(resource, sprites, IsSpriteConfigured);
        }

        public SoundResource ResolveSound(SoundResource resource)
        {
            return ResolveResource(resource, sounds, IsSoundConfigured);
        }

        public SceneResource ResolveScene(SceneResource resource)
        {
            if (resource == null)
                return TryGetFirst(scenes, out SceneResource first) ? first : null;

            string normalizedId = NormalizeId(resource.Id);
            if (!string.IsNullOrEmpty(normalizedId) && TryGetById(scenes, normalizedId, out SceneResource byId))
                return byId;

            if (resource.TryApplySceneAssetName())
                return resource;

            if (!string.IsNullOrWhiteSpace(resource.SceneName))
                return resource;

            return TryGetFirst(scenes, out SceneResource fallback) ? fallback : resource;
        }

        private static T ResolveResource<T>(T resource, List<T> resources, Func<T, bool> isConfigured)
            where T : ResourceEntry
        {
            if (isConfigured(resource))
                return resource;

            if (resource != null)
            {
                string normalizedId = NormalizeId(resource.Id);
                if (!string.IsNullOrEmpty(normalizedId) && TryGetById(resources, normalizedId, out T byId))
                    return byId;
            }

            return TryGetFirst(resources, out T first) ? first : resource;
        }

        private static bool TryGetByIdOrFirst<T>(List<T> resources, string id, out T resource)
            where T : ResourceEntry
        {
            if (TryGetById(resources, id, out resource))
                return true;

            return string.IsNullOrEmpty(NormalizeId(id)) && TryGetFirst(resources, out resource);
        }

        private static bool TryGetById<T>(IEnumerable<T> resources, string id, out T resource)
            where T : ResourceEntry
        {
            resource = null;
            string normalizedId = NormalizeId(id);
            if (string.IsNullOrEmpty(normalizedId))
                return false;

            foreach (T item in resources)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                    continue;

                if (string.Equals(item.Id.Trim(), normalizedId, StringComparison.Ordinal))
                {
                    resource = item;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetFirst<T>(IEnumerable<T> resources, out T resource)
            where T : ResourceEntry
        {
            resource = null;

            foreach (T item in resources)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                    continue;

                resource = item;
                return true;
            }

            return false;
        }

        private static bool IsUIConfigured(UIResource resource)
        {
            return resource != null && resource.Prefab != null;
        }

        private static bool IsSpriteConfigured(SpriteResource resource)
        {
            return resource != null && resource.Sprite != null;
        }

        private static bool IsSoundConfigured(SoundResource resource)
        {
            return resource != null && resource.Clip != null;
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (SceneResource scene in scenes)
            {
                scene?.SyncSceneNameFromAsset();
            }
        }
#endif

        [Serializable]
        public abstract class ResourceEntry
        {
            [SerializeField] private string id;

            public string Id => id;
        }

        [Serializable]
        public sealed class UIResource : ResourceEntry
        {
            [SerializeField] private GameObject prefab;

            public GameObject Prefab => prefab;
        }

        [Serializable]
        public sealed class SpriteResource : ResourceEntry
        {
            [SerializeField] private Sprite sprite;

            public Sprite Sprite => sprite;
        }

        [Serializable]
        public sealed class SoundResource : ResourceEntry
        {
            [SerializeField] private SoundCategory category = SoundCategory.SFX;
            [SerializeField] private AudioClip clip;
            [SerializeField] private bool loop;
            [Range(0f, 1f)] [SerializeField] private float volumeScale = 1f;

            public SoundCategory Category => category;
            public AudioClip Clip => clip;
            public bool Loop => loop;
            public float VolumeScale => volumeScale;
        }

        [Serializable]
        public sealed class SceneResource : ResourceEntry
        {
#if UNITY_EDITOR
            [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif
            [SerializeField] private string sceneName;

            public string SceneName => sceneName;

            /// <summary>编辑器中把场景资源名写入 sceneName，便于打包后与编辑器一致。</summary>
            public void SyncSceneNameFromAsset()
            {
#if UNITY_EDITOR
                if (sceneAsset != null)
                    sceneName = sceneAsset.name;
#endif
            }

            /// <summary>按优先级取场景名：Id 查库 → 场景资源 → 场景名。</summary>
            internal bool TryApplySceneAssetName()
            {
#if UNITY_EDITOR
                if (sceneAsset == null)
                    return false;

                sceneName = sceneAsset.name;
                return true;
#else
                return false;
#endif
            }
        }

        public enum SoundCategory
        {
            Music,
            SFX
        }
    }
}

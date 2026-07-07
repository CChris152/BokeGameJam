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
            return TryGetById(uiPrefabs, id, out resource);
        }

        public bool TryGetSprite(string id, out SpriteResource resource)
        {
            return TryGetById(sprites, id, out resource);
        }

        public bool TryGetSound(string id, out SoundResource resource)
        {
            return TryGetById(sounds, id, out resource);
        }

        public bool TryGetScene(string id, out SceneResource resource)
        {
            return TryGetById(scenes, id, out resource);
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

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (SceneResource scene in scenes)
            {
                scene?.RefreshSceneName();
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

#if UNITY_EDITOR
            internal void RefreshSceneName()
            {
                if (sceneAsset != null)
                    sceneName = sceneAsset.name;
            }
#endif
        }

        public enum SoundCategory
        {
            Music,
            SFX
        }
    }
}

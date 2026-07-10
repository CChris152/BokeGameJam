using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// World-B-only clue marker. Art can replace the fallback TextMesh by using
    /// a prefab with this component and any Renderer children.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Level3WorldHint : MonoBehaviour
    {
        [SerializeField] private string clueText;
        [SerializeField] private Renderer[] clueRenderers;

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnEnable()
        {
            EventManager.On<WorldId>(GameEvents.ActiveWorldChanged, OnWorldChanged);
            OnWorldChanged(GameManager.Instance != null
                ? GameManager.Instance.ActiveWorld
                : WorldId.A);
        }

        private void OnDisable()
        {
            EventManager.Off<WorldId>(GameEvents.ActiveWorldChanged, OnWorldChanged);
        }

        public void Initialize(string text)
        {
            clueText = text ?? string.Empty;
            TextMesh label = GetComponentInChildren<TextMesh>(true);
            if (label != null)
                label.text = clueText;

            CacheRenderers();
            OnWorldChanged(GameManager.Instance != null
                ? GameManager.Instance.ActiveWorld
                : WorldId.A);
        }

        private void CacheRenderers()
        {
            if (clueRenderers == null || clueRenderers.Length == 0)
                clueRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnWorldChanged(WorldId world)
        {
            CacheRenderers();
            bool visible = world == WorldId.B;
            for (int i = 0; i < clueRenderers.Length; i++)
            {
                if (clueRenderers[i] != null)
                    clueRenderers[i].enabled = visible;
            }
        }
    }
}

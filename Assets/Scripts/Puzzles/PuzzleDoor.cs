using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public sealed class PuzzleDoor : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool startsOpen;

        [Header("Blocking")]
        [SerializeField] private Collider2D[] blockingColliders;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField] private bool hideRenderersWhenOpen;
        [SerializeField] private Color closedColor = Color.white;
        [SerializeField] private Color openColor = new(0.4f, 1f, 0.7f, 0.45f);

        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (blockingColliders == null || blockingColliders.Length == 0)
                blockingColliders = GetComponentsInChildren<Collider2D>(true);

            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            SetOpen(startsOpen);
        }

        public void SetOpen(bool value)
        {
            isOpen = value;
            ApplyBlocking();
            ApplyVisual();
        }

        private void ApplyBlocking()
        {
            if (blockingColliders == null)
                return;

            for (int i = 0; i < blockingColliders.Length; i++)
            {
                if (blockingColliders[i] != null)
                    blockingColliders[i].enabled = !isOpen;
            }
        }

        private void ApplyVisual()
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                    continue;

                if (hideRenderersWhenOpen)
                    sr.enabled = !isOpen;
                else
                    sr.color = isOpen ? openColor : closedColor;
            }
        }
    }
}

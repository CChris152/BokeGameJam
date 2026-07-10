using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// One Level 3 mirror shard. It is neutral in World A, color-coded in World B,
    /// and remains unavailable until the clock cabinet is unlocked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Level3MirrorShard : InteractableObject
    {
        [Header("Shard")]
        [SerializeField] private MirrorShardColor colorKind;
        [SerializeField] private Color worldAColor = new(0.72f, 0.78f, 0.82f, 1f);
        [SerializeField] private Color yellowColor = new(1f, 0.82f, 0.2f, 1f);
        [SerializeField] private Color redColor = new(0.95f, 0.25f, 0.22f, 1f);
        [SerializeField] private Color greenColor = new(0.25f, 0.9f, 0.4f, 1f);
        [SerializeField] private Sprite worldASprite;
        [SerializeField] private Sprite worldBSprite;
        [SerializeField] private bool tintArtForWorld = true;

        private Level3PuzzleController controller;
        private SpriteRenderer[] renderers;
        private Vector3 origin;
        private bool available;
        private bool delivered;

        public MirrorShardColor ColorKind => colorKind;
        public Vector3 Origin => origin;
        public bool IsDelivered => delivered;

        protected override void Awake()
        {
            base.Awake();
            origin = transform.position;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (Col != null)
                Col.isTrigger = true;
        }

        private void OnEnable()
        {
            EventManager.On<WorldId>(GameEvents.ActiveWorldChanged, OnWorldChanged);
            ApplyWorldVisual(CurrentWorld);
            ApplyAvailability();
        }

        private void OnDisable()
        {
            EventManager.Off<WorldId>(GameEvents.ActiveWorldChanged, OnWorldChanged);
        }

        public void Initialize(Level3PuzzleController owner, MirrorShardColor color)
        {
            controller = owner;
            colorKind = color;
            origin = transform.position;
            name = $"MirrorShard_{colorKind}";
            ApplyWorldVisual(CurrentWorld);
            ApplyAvailability();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            return available
                && !delivered
                && controller != null
                && controller.CanCollectShards
                && base.CanInteract(interactor);
        }

        public void SetAvailable(bool value)
        {
            available = value;
            ApplyAvailability();
        }

        public void MarkDelivered()
        {
            delivered = true;
            available = false;
            ApplyAvailability();
        }

        public void RestoreToOrigin()
        {
            delivered = false;
            available = true;
            Drop(origin);
            ApplyWorldVisual(CurrentWorld);
            ApplyAvailability();
        }

        private WorldId CurrentWorld =>
            GameManager.Instance != null ? GameManager.Instance.ActiveWorld : WorldId.A;

        private void OnWorldChanged(WorldId world)
        {
            ApplyWorldVisual(world);
        }

        private void ApplyAvailability()
        {
            bool visible = available && !delivered;
            if (renderers == null)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }

            if (Col != null)
                Col.enabled = visible;
        }

        private void ApplyWorldVisual(WorldId world)
        {
            if (renderers == null)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            Color tint = world == WorldId.A ? worldAColor : GetWorldBColor();
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (renderer == SpriteRenderer)
                {
                    Sprite sprite = world == WorldId.A ? worldASprite : worldBSprite;
                    if (sprite != null)
                        renderer.sprite = sprite;
                }

                if (tintArtForWorld)
                    renderer.color = tint;
            }
        }

        private Color GetWorldBColor()
        {
            return colorKind switch
            {
                MirrorShardColor.Yellow => yellowColor,
                MirrorShardColor.Red => redColor,
                MirrorShardColor.Green => greenColor,
                _ => worldAColor
            };
        }
    }
}

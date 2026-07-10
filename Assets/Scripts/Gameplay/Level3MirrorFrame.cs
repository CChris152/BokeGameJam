using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// Delivery point for the Level 3 mirror sequence. World B reveals the
    /// currently required color; World A keeps the frame visually neutral.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Level3MirrorFrame : InteractableObjectC
    {
        [Header("Level 3 Frame")]
        [SerializeField] private Color worldAColor = new(0.45f, 0.48f, 0.52f, 1f);
        [SerializeField] private Color yellowColor = new(1f, 0.82f, 0.2f, 1f);
        [SerializeField] private Color redColor = new(0.95f, 0.25f, 0.22f, 1f);
        [SerializeField] private Color greenColor = new(0.25f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color assembledColor = new(0.75f, 0.9f, 1f, 1f);
        [SerializeField] private bool tintArtForWorld = true;

        private Level3PuzzleController controller;
        private SpriteRenderer[] renderers;
        private bool available;
        private bool sequenceComplete;

        protected override void Awake()
        {
            base.Awake();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (Col != null)
                Col.isTrigger = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventManager.On<WorldId>(GameEvents.ActiveWorldChanged, OnWorldChanged);
            ApplyFrameVisual();
            ApplyAvailability();
        }

        protected override void OnDisable()
        {
            EventManager.Off<WorldId>(GameEvents.ActiveWorldChanged, OnWorldChanged);
            base.OnDisable();
        }

        public void Initialize(Level3PuzzleController owner)
        {
            controller = owner;
            name = "Level3MirrorFrame";
            ApplyFrameVisual();
            ApplyAvailability();
        }

        public void SetAvailable(bool value)
        {
            available = value;
            ApplyAvailability();
        }

        public void MarkSequenceComplete()
        {
            sequenceComplete = true;
            ApplyFrameVisual();
        }

        public void RefreshTargetVisual()
        {
            ApplyFrameVisual();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (!available || controller == null)
                return false;

            if (controller.IsMirrorAssemblyReady)
                return true;

            return controller.CanCollectShards
                && interactor != null
                && interactor.HeldItem is Level3MirrorShard;
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            if (controller.IsMirrorAssemblyReady)
            {
                controller.OpenMirrorAssembly();
                return;
            }

            if (interactor.HeldItem is Level3MirrorShard shard)
                controller.DeliverShard(interactor, shard);
        }

        protected override void RefreshVisual()
        {
            ApplyFrameVisual();
        }

        protected override void OnHeldItemChanged(HeldItemInfo info)
        {
            ApplyFrameVisual();
        }

        protected override void OnMechanismSatisfied(string mechanismId)
        {
        }

        private void OnWorldChanged(WorldId world)
        {
            ApplyFrameVisual();
        }

        private void ApplyAvailability()
        {
            if (renderers == null)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = available;
            }

            if (Col != null)
                Col.enabled = available;
        }

        private void ApplyFrameVisual()
        {
            if (!tintArtForWorld)
                return;

            if (renderers == null)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            Color color = ResolveColor();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = color;
            }
        }

        private Color ResolveColor()
        {
            if (sequenceComplete)
                return assembledColor;

            WorldId world = GameManager.Instance != null
                ? GameManager.Instance.ActiveWorld
                : WorldId.A;
            if (world == WorldId.A || controller == null)
                return worldAColor;

            return controller.ExpectedShardColor switch
            {
                MirrorShardColor.Yellow => yellowColor,
                MirrorShardColor.Red => redColor,
                MirrorShardColor.Green => greenColor,
                _ => worldAColor
            };
        }
    }
}

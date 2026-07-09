using BokeGameJam.Gameplay;
using UnityEngine;

namespace BokeGameJam.Puzzles
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class PuzzleStateEmitter : MonoBehaviour, IInteractable
    {
        [Header("State")]
        [SerializeField] private string puzzleId = "puzzle";
        [SerializeField] private string stateName = "switch";
        [SerializeField] private bool initialState;
        [SerializeField] private bool toggleOnInteract = true;
        [SerializeField] private bool setValueWhenInteracted = true;
        [SerializeField] private bool canRepeatSet = true;
        [SerializeField] private bool emitInitialState = true;

        [Header("Visual")]
        [SerializeField] private bool updateVisual = true;
        [SerializeField] private Color offColor = Color.white;
        [SerializeField] private Color onColor = new(0.45f, 0.9f, 0.55f, 1f);
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Sprite onSprite;

        private bool currentState;
        private SpriteRenderer[] spriteRenderers;

        public bool CurrentState => currentState;
        public Vector3 InteractionPosition => transform.position;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null && !trigger.isTrigger)
            {
                Debug.LogWarning(
                    $"[PuzzleStateEmitter] '{name}' 的 Collider2D 建议设为 Trigger，以便玩家进入交互范围。",
                    this);
            }

            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnEnable()
        {
            currentState = initialState;
            ApplyVisual();
            PuzzleStateHub.SetState(puzzleId, stateName, currentState, gameObject, emitInitialState);
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            if (!enabled || !gameObject.activeInHierarchy)
                return false;

            return toggleOnInteract || canRepeatSet || currentState != setValueWhenInteracted;
        }

        public void SetInInteractRange(bool inRange)
        {
        }

        public bool Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return false;

            currentState = toggleOnInteract ? !currentState : setValueWhenInteracted;
            ApplyVisual();
            PuzzleStateHub.SetState(puzzleId, stateName, currentState, gameObject);
            return true;
        }

        public void SetState(bool value, bool emit = true)
        {
            currentState = value;
            ApplyVisual();
            PuzzleStateHub.SetState(puzzleId, stateName, currentState, gameObject, emit);
        }

        private void ApplyVisual()
        {
            if (!updateVisual || spriteRenderers == null)
                return;

            Color color = currentState ? onColor : offColor;
            Sprite sprite = currentState ? onSprite : offSprite;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (sr == null)
                    continue;

                sr.color = color;
                if (sprite != null)
                    sr.sprite = sprite;
            }
        }
    }
}

using BokeGameJam.Gameplay;
using UnityEngine;

namespace BokeGameJam.Puzzles
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class PuzzleInteractEmitter : MonoBehaviour, IInteractable
    {
        [Header("Signal")]
        [SerializeField] private string puzzleId = string.Empty;
        [SerializeField] private string signalName = "puzzle_signal";
        [SerializeField] private bool canRepeat = true;

        private bool hasEmitted;

        public Vector3 InteractionPosition => transform.position;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null && !trigger.isTrigger)
            {
                Debug.LogWarning(
                    $"[PuzzleInteractEmitter] '{name}' 的 Collider2D 建议设为 Trigger，以便玩家进入交互范围。",
                    this);
            }
        }

        public void SetInInteractRange(bool inRange)
        {
            // Hook for prompt/highlight visuals. Kept empty so art prefabs can opt in later.
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return enabled && gameObject.activeInHierarchy && (canRepeat || !hasEmitted);
        }

        public bool Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return false;

            hasEmitted = true;
            PuzzleSignalHub.Emit(signalName, gameObject, puzzleId);
            return true;
        }
    }
}

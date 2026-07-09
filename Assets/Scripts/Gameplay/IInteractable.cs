using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// Common player interaction contract. Pickup objects, puzzle switches, code panels,
    /// sockets, and future interactables can all plug into PlayerInteractor through this.
    /// </summary>
    public interface IInteractable
    {
        Vector3 InteractionPosition { get; }
        bool CanInteract(PlayerInteractor interactor);
        void SetInInteractRange(bool inRange);
        bool Interact(PlayerInteractor interactor);
    }
}

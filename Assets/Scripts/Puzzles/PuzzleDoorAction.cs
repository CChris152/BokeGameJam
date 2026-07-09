using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public sealed class PuzzleDoorAction : PuzzleAction
    {
        [Header("Door")]
        [SerializeField] private PuzzleDoor targetDoor;
        [SerializeField] private GameObject doorObject;
        [SerializeField] private bool open;

        public bool IsOpen => open;

        public override void Execute(PuzzleActionContext context)
        {
            PuzzleDoor door = ResolveDoor();
            if (door != null)
                door.SetOpen(open);
        }

        private PuzzleDoor ResolveDoor()
        {
            if (targetDoor != null)
                return targetDoor;

            GameObject target = doorObject != null ? doorObject : gameObject;
            targetDoor = target.GetComponent<PuzzleDoor>();
            if (targetDoor != null)
                return targetDoor;

            targetDoor = target.AddComponent<PuzzleDoor>();
            return targetDoor;
        }
    }
}

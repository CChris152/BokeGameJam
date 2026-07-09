using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public sealed class SetActivePuzzleAction : PuzzleAction
    {
        [SerializeField] private GameObject target;
        [SerializeField] private bool active;

        public override void Execute(PuzzleActionContext context)
        {
            GameObject resolvedTarget = target != null ? target : gameObject;
            resolvedTarget.SetActive(active);
        }
    }
}

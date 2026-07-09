using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public sealed class ColliderPuzzleAction : PuzzleAction
    {
        [SerializeField] private Collider2D[] targets;
        [SerializeField] private bool enabledState;

        public override void Execute(PuzzleActionContext context)
        {
            Collider2D[] resolvedTargets = targets;
            if (resolvedTargets == null || resolvedTargets.Length == 0)
                resolvedTargets = GetComponents<Collider2D>();

            for (int i = 0; i < resolvedTargets.Length; i++)
            {
                if (resolvedTargets[i] != null)
                    resolvedTargets[i].enabled = enabledState;
            }
        }
    }
}

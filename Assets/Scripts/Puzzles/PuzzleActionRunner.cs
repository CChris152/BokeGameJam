using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public sealed class PuzzleActionRunner : MonoBehaviour
    {
        [SerializeField] private PuzzleAction[] actions;
        [SerializeField] private bool runOnlyOnce;

        private bool hasRun;

        public void Run(PuzzleActionContext context)
        {
            if (runOnlyOnce && hasRun)
                return;

            hasRun = true;

            PuzzleAction[] resolvedActions = actions;
            if (resolvedActions == null || resolvedActions.Length == 0)
                resolvedActions = GetComponents<PuzzleAction>();

            for (int i = 0; i < resolvedActions.Length; i++)
            {
                if (resolvedActions[i] != null)
                    resolvedActions[i].Execute(context);
            }
        }

        public void ResetRunner()
        {
            hasRun = false;
        }
    }
}

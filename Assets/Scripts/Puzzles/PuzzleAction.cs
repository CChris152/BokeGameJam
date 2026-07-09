using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public abstract class PuzzleAction : MonoBehaviour
    {
        public abstract void Execute(PuzzleActionContext context);
    }

    public readonly struct PuzzleActionContext
    {
        public readonly PuzzleSignal Signal;
        public readonly GameObject RuleObject;

        public PuzzleActionContext(PuzzleSignal signal, GameObject ruleObject)
        {
            Signal = signal;
            RuleObject = ruleObject;
        }
    }
}

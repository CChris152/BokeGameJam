using System;
using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public sealed class PuzzleStateRule : MonoBehaviour
    {
        [Serializable]
        public struct RequiredState
        {
            public string stateName;
            public bool requiredValue;
        }

        [Header("State Check")]
        [SerializeField] private string puzzleId = "puzzle";
        [SerializeField] private RequiredState[] requiredStates;
        [SerializeField] private bool evaluateOnEnable = true;
        [SerializeField] private bool runOnlyWhenResultChanges;

        [Header("Result")]
        [SerializeField] private PuzzleActionRunner onCorrect;
        [SerializeField] private PuzzleActionRunner onIncorrect;

        private bool hasLastResult;
        private bool lastResult;

        private void OnEnable()
        {
            EventManager.On<PuzzleStateChange>(PuzzleStateHub.StateChanged, OnStateChanged);

            if (evaluateOnEnable)
                Evaluate(null);
        }

        private void OnDisable()
        {
            EventManager.Off<PuzzleStateChange>(PuzzleStateHub.StateChanged, OnStateChanged);
        }

        public void EvaluateNow()
        {
            Evaluate(null);
        }

        private void OnStateChanged(PuzzleStateChange change)
        {
            if (!string.Equals(Normalize(puzzleId), change.PuzzleId, StringComparison.Ordinal))
                return;

            Evaluate(change);
        }

        private void Evaluate(PuzzleStateChange? change)
        {
            if (requiredStates == null || requiredStates.Length == 0)
                return;

            bool correct = true;
            for (int i = 0; i < requiredStates.Length; i++)
            {
                RequiredState required = requiredStates[i];
                string stateName = Normalize(required.stateName);
                if (string.IsNullOrEmpty(stateName))
                    continue;

                bool current = PuzzleStateHub.GetState(puzzleId, stateName);
                if (current == required.requiredValue)
                    continue;

                correct = false;
                break;
            }

            if (runOnlyWhenResultChanges && hasLastResult && lastResult == correct)
                return;

            bool resultChanged = !hasLastResult || lastResult != correct;
            hasLastResult = true;
            lastResult = correct;

            GameObject source = change.HasValue ? change.Value.Source : null;
            string signalName = correct ? "state_correct" : "state_incorrect";
            PuzzleActionContext context = new(new PuzzleSignal(signalName, source), gameObject);

            if (correct)
                onCorrect?.Run(context);
            else
                onIncorrect?.Run(context);

            if (change.HasValue && resultChanged)
                PlayResultSfx(correct);
        }

        private static void PlayResultSfx(bool correct)
        {
            if (GameAudioManager.Instance == null)
                return;

            if (correct)
            {
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.PuzzleSuccess);
                return;
            }

            GameAudioManager.Instance.PlayRandomSFXByResourcePaths(
                1f,
                GameSfxPaths.PuzzleFailure1,
                GameSfxPaths.PuzzleFailure3);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

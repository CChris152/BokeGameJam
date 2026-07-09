using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public sealed class PuzzleSequenceRule : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField] private string puzzleId = string.Empty;
        [SerializeField] private string[] expectedSignals = { "A", "B", "C" };
        [SerializeField] private bool resetOnWrongSignal = true;
        [SerializeField] private bool allowOverlapRestart = true;
        [SerializeField] private float timeoutSeconds;

        [Header("Result")]
        [SerializeField] private PuzzleActionRunner onProgress;
        [SerializeField] private PuzzleActionRunner onSolved;
        [SerializeField] private PuzzleActionRunner onFailed;
        [SerializeField] private bool solveOnlyOnce = true;
        [SerializeField] private bool resetAfterSolved;

        private int progressIndex;
        private bool solved;
        private float lastAcceptedTime;
        private PuzzleSignal lastSignal;

        private void OnEnable()
        {
            EventManager.On<PuzzleSignal>(PuzzleSignalHub.SignalEmitted, OnSignalEmitted);
        }

        private void OnDisable()
        {
            EventManager.Off<PuzzleSignal>(PuzzleSignalHub.SignalEmitted, OnSignalEmitted);
        }

        private void Update()
        {
            if (timeoutSeconds <= 0f || progressIndex <= 0)
                return;

            if (Time.time - lastAcceptedTime <= timeoutSeconds)
                return;

            ResetProgress();
            RunFailure(lastSignal);
        }

        public void ResetProgress()
        {
            progressIndex = 0;
            lastAcceptedTime = 0f;
        }

        public void ResetSolvedState()
        {
            solved = false;
            ResetProgress();
            if (onSolved != null)
                onSolved.ResetRunner();
            if (onFailed != null)
                onFailed.ResetRunner();
            if (onProgress != null)
                onProgress.ResetRunner();
        }

        private void OnSignalEmitted(PuzzleSignal signal)
        {
            if (!MatchesPuzzle(signal))
                return;

            if (solveOnlyOnce && solved)
                return;

            if (expectedSignals == null || expectedSignals.Length == 0)
                return;

            string incoming = signal.SignalName;
            string expected = Normalize(expectedSignals[progressIndex]);
            if (incoming == expected)
            {
                AcceptSignal(signal);
                return;
            }

            if (!resetOnWrongSignal)
                return;

            ResetProgress();
            RunFailure(signal);

            string first = Normalize(expectedSignals[0]);
            if (allowOverlapRestart && incoming == first)
                AcceptSignal(signal);
        }

        private void AcceptSignal(PuzzleSignal signal)
        {
            lastSignal = signal;
            lastAcceptedTime = Time.time;
            progressIndex++;

            if (progressIndex < expectedSignals.Length)
            {
                onProgress?.Run(new PuzzleActionContext(signal, gameObject));
                return;
            }

            solved = true;
            onSolved?.Run(new PuzzleActionContext(signal, gameObject));

            if (resetAfterSolved && !solveOnlyOnce)
                ResetProgress();
        }

        private void RunFailure(PuzzleSignal signal)
        {
            onFailed?.Run(new PuzzleActionContext(signal, gameObject));
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private bool MatchesPuzzle(PuzzleSignal signal)
        {
            string normalizedPuzzleId = Normalize(puzzleId);
            return string.IsNullOrEmpty(normalizedPuzzleId)
                || string.Equals(normalizedPuzzleId, signal.PuzzleId, System.StringComparison.Ordinal);
        }
    }
}

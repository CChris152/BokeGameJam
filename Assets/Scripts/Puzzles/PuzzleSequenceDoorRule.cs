using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public sealed class PuzzleSequenceDoorRule : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField] private string puzzleId = "door_1";
        [SerializeField] private string[] expectedSignals = { "A", "B", "C" };
        [SerializeField] private bool allowOverlapRestart = true;

        [Header("Door")]
        [SerializeField] private PuzzleDoor targetDoor;
        [SerializeField] private bool closeDoorOnEnable = true;
        [SerializeField] private bool closeDoorOnProgress = true;
        [SerializeField] private bool closeDoorOnWrong = true;
        [SerializeField] private bool solveOnlyOnce;
        [SerializeField] private bool resetAfterSolved = true;

        private int progressIndex;
        private bool solved;

        private void OnEnable()
        {
            EventManager.On<PuzzleSignal>(PuzzleSignalHub.SignalEmitted, OnSignalEmitted);

            if (closeDoorOnEnable)
                SetDoorOpen(false);
        }

        private void OnDisable()
        {
            EventManager.Off<PuzzleSignal>(PuzzleSignalHub.SignalEmitted, OnSignalEmitted);
        }

        public void ResetProgress()
        {
            progressIndex = 0;
        }

        public void ResetSolvedState()
        {
            solved = false;
            ResetProgress();
        }

        private void OnSignalEmitted(PuzzleSignal signal)
        {
            if (!MatchesPuzzle(signal))
                return;

            if (solveOnlyOnce && solved)
                return;

            if (expectedSignals == null || expectedSignals.Length == 0)
                return;

            string incoming = Normalize(signal.SignalName);
            string expected = Normalize(expectedSignals[progressIndex]);
            if (incoming == expected)
            {
                AcceptSignal(incoming);
                return;
            }

            ResetProgress();
            if (closeDoorOnWrong)
                SetDoorOpen(false);

            string first = Normalize(expectedSignals[0]);
            if (allowOverlapRestart && incoming == first)
                AcceptSignal(incoming);
        }

        private void AcceptSignal(string incoming)
        {
            progressIndex++;
            if (progressIndex < expectedSignals.Length)
            {
                if (closeDoorOnProgress)
                    SetDoorOpen(false);
                return;
            }

            solved = true;
            SetDoorOpen(true);

            if (resetAfterSolved && !solveOnlyOnce)
                ResetProgress();
        }

        private void SetDoorOpen(bool open)
        {
            if (targetDoor != null)
                targetDoor.SetOpen(open);
        }

        private bool MatchesPuzzle(PuzzleSignal signal)
        {
            string normalizedPuzzleId = Normalize(puzzleId);
            return string.IsNullOrEmpty(normalizedPuzzleId)
                || string.Equals(normalizedPuzzleId, signal.PuzzleId, System.StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public static class PuzzleSignalHub
    {
        public const string SignalEmitted = "Puzzle.SignalEmitted";

        public static void Emit(string signalName, GameObject source)
        {
            Emit(signalName, source, string.Empty);
        }

        public static void Emit(string signalName, GameObject source, string puzzleId)
        {
            if (string.IsNullOrWhiteSpace(signalName))
            {
                Debug.LogWarning("[PuzzleSignalHub] Signal name is empty.", source);
                return;
            }

            string normalizedPuzzleId = string.IsNullOrWhiteSpace(puzzleId) ? string.Empty : puzzleId.Trim();
            EventManager.Emit(SignalEmitted, new PuzzleSignal(signalName.Trim(), source, normalizedPuzzleId));
        }
    }

    public readonly struct PuzzleSignal
    {
        public readonly string PuzzleId;
        public readonly string SignalName;
        public readonly GameObject Source;

        public PuzzleSignal(string signalName, GameObject source)
            : this(signalName, source, string.Empty)
        {
        }

        public PuzzleSignal(string signalName, GameObject source, string puzzleId)
        {
            PuzzleId = puzzleId ?? string.Empty;
            SignalName = signalName;
            Source = source;
        }
    }
}

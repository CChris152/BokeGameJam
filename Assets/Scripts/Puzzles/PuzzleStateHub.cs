using System.Collections.Generic;
using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.Puzzles
{
    public static class PuzzleStateHub
    {
        public const string StateChanged = "Puzzle.StateChanged";

        private static readonly Dictionary<string, Dictionary<string, bool>> states = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            states.Clear();
        }

        public static void SetState(string puzzleId, string stateName, bool value, GameObject source, bool emit = true)
        {
            string normalizedPuzzleId = Normalize(puzzleId);
            string normalizedStateName = Normalize(stateName);
            if (string.IsNullOrEmpty(normalizedPuzzleId) || string.IsNullOrEmpty(normalizedStateName))
            {
                Debug.LogWarning("[PuzzleStateHub] Puzzle id or state name is empty.", source);
                return;
            }

            if (!states.TryGetValue(normalizedPuzzleId, out Dictionary<string, bool> puzzleStates))
            {
                puzzleStates = new Dictionary<string, bool>();
                states[normalizedPuzzleId] = puzzleStates;
            }

            puzzleStates[normalizedStateName] = value;

            if (emit)
                EventManager.Emit(StateChanged, new PuzzleStateChange(normalizedPuzzleId, normalizedStateName, value, source));
        }

        public static bool TryGetState(string puzzleId, string stateName, out bool value)
        {
            value = false;
            string normalizedPuzzleId = Normalize(puzzleId);
            string normalizedStateName = Normalize(stateName);

            return !string.IsNullOrEmpty(normalizedPuzzleId)
                && !string.IsNullOrEmpty(normalizedStateName)
                && states.TryGetValue(normalizedPuzzleId, out Dictionary<string, bool> puzzleStates)
                && puzzleStates.TryGetValue(normalizedStateName, out value);
        }

        public static bool GetState(string puzzleId, string stateName, bool defaultValue = false)
        {
            return TryGetState(puzzleId, stateName, out bool value) ? value : defaultValue;
        }

        public static void ClearPuzzle(string puzzleId)
        {
            string normalizedPuzzleId = Normalize(puzzleId);
            if (!string.IsNullOrEmpty(normalizedPuzzleId))
                states.Remove(normalizedPuzzleId);
        }

        public static void ClearAll()
        {
            states.Clear();
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public readonly struct PuzzleStateChange
    {
        public readonly string PuzzleId;
        public readonly string StateName;
        public readonly bool Value;
        public readonly GameObject Source;

        public PuzzleStateChange(string puzzleId, string stateName, bool value, GameObject source)
        {
            PuzzleId = puzzleId;
            StateName = stateName;
            Value = value;
            Source = source;
        }
    }
}

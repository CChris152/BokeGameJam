using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Handles level selection button registration for SelectScene.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private LevelSelectedEvent onLevelSelected = new();

        public LevelSelectedEvent OnLevelSelected => onLevelSelected;

        private void Awake()
        {
            RegisterButtons();
        }

        private void OnDestroy()
        {
            UnregisterButtons();
        }

        public void RegisterLevelSelectedAction(UnityAction<int> action)
        {
            if (action == null)
                return;

            onLevelSelected.RemoveListener(action);
            onLevelSelected.AddListener(action);
        }

        public void UnregisterLevelSelectedAction(UnityAction<int> action)
        {
            if (action == null)
                return;

            onLevelSelected.RemoveListener(action);
        }

        private void RegisterButtons()
        {
            if (levelButtons == null)
                return;

            for (int i = 0; i < levelButtons.Length; i++)
            {
                Button button = levelButtons[i];
                if (button == null)
                {
                    Debug.LogWarning($"[LevelSelectController] Level button at index {i} is missing.", this);
                    continue;
                }

                int levelIndex = i + 1;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => HandleLevelSelected(levelIndex));
            }
        }

        private void UnregisterButtons()
        {
            if (levelButtons == null)
                return;

            foreach (Button button in levelButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
        }

        private void HandleLevelSelected(int levelIndex)
        {
            onLevelSelected.Invoke(levelIndex);
        }

        [System.Serializable]
        public class LevelSelectedEvent : UnityEvent<int>
        {
        }
    }
}

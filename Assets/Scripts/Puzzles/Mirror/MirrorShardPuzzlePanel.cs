using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.Puzzles.Mirror
{
    public sealed class MirrorShardPuzzlePanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private MirrorShardPuzzlePiece[] pieces;
        [SerializeField] private Button closeButton;

        [Header("Open Animation")]
        [SerializeField] private float openDuration = 0.2f;
        [SerializeField] private Vector3 closedScale = new(0.92f, 0.92f, 1f);
        [SerializeField] private Vector3 openScale = Vector3.one;

        [Header("Solved")]
        [SerializeField] private float solvedCloseDelay = 0.25f;
        [SerializeField] private bool disableInputWhenSolved = true;

        private string puzzleId;
        private bool isOpen;
        private bool isSolved;
        private Coroutine openRoutine;

        public event Action Solved;
        public event Action Closed;

        public string PuzzleId => puzzleId;
        public bool IsSolved => isSolved;

        private void Awake()
        {
            ResolveReferences();

            if (closeButton != null)
                closeButton.onClick.AddListener(RequestClose);
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(RequestClose);
        }

        private void Update()
        {
            if (isOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                RequestClose();
        }

        public void Open(string ownerPuzzleId)
        {
            puzzleId = string.IsNullOrWhiteSpace(ownerPuzzleId) ? string.Empty : ownerPuzzleId.Trim();
            isOpen = true;
            isSolved = false;

            ResolveReferences();
            SetInteractable(true);

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null)
                {
                    pieces[i].Owner = this;
                    pieces[i].ResetPiece();
                }
            }

            PlayOpenAnimation();
        }

        public void NotifyPieceChanged()
        {
            if (!isOpen || isSolved)
                return;

            if (pieces == null || pieces.Length == 0)
            {
                Debug.LogWarning("[MirrorShardPuzzlePanel] No pieces assigned.", this);
                return;
            }

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null || !pieces[i].IsPlaced)
                    return;
            }

            CompletePuzzle();
        }

        public void RequestClose()
        {
            if (!isOpen)
                return;

            isOpen = false;
            Closed?.Invoke();
        }

        private void CompletePuzzle()
        {
            isSolved = true;

            if (disableInputWhenSolved)
                SetInteractable(false);

            Solved?.Invoke();

            if (isOpen && solvedCloseDelay > 0f)
                StartCoroutine(CloseAfterDelay());
        }

        private IEnumerator CloseAfterDelay()
        {
            yield return new WaitForSecondsRealtime(solvedCloseDelay);
            RequestClose();
        }

        private void ResolveReferences()
        {
            if (panelRoot == null)
                panelRoot = transform as RectTransform;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            if (pieces == null || pieces.Length == 0)
                pieces = GetComponentsInChildren<MirrorShardPuzzlePiece>(true);
        }

        private void SetInteractable(bool value)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        private void PlayOpenAnimation()
        {
            if (openRoutine != null)
                StopCoroutine(openRoutine);

            openRoutine = StartCoroutine(OpenAnimation());
        }

        private IEnumerator OpenAnimation()
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (panelRoot != null)
                panelRoot.localScale = closedScale;

            float duration = Mathf.Max(0.01f, openDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                if (canvasGroup != null)
                    canvasGroup.alpha = eased;

                if (panelRoot != null)
                    panelRoot.localScale = Vector3.LerpUnclamped(closedScale, openScale, eased);

                yield return null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;

            if (panelRoot != null)
                panelRoot.localScale = openScale;

            openRoutine = null;
        }
    }
}

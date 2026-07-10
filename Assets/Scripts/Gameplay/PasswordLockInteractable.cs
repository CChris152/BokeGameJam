using System;
using System.Text;
using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.Events;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// Player-facing password lock. UI is requested through an event so gameplay
    /// validation stays independent from the presentation implementation.
    /// </summary>
    public sealed class PasswordLockInteractable : InteractableObject
    {
        [Header("Password")]
        [SerializeField] private string expectedCode = "135689";
        [SerializeField] private bool unlocked;

        [Header("Visual State")]
        [SerializeField] private Color lockedColor = new(0.55f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color inRangeColor = new(0.95f, 0.72f, 0.22f, 1f);
        [SerializeField] private Color unlockedColor = new(0.22f, 0.65f, 0.35f, 1f);
        [SerializeField] private GameObject[] hideOnUnlocked;
        [SerializeField] private GameObject[] showOnUnlocked;

        [Header("Audio Hooks")]
        [Tooltip("Optional ResourceDefinitionDatabase SFX id used when no clip override is assigned.")]
        [SerializeField] private string digitSfxId;
        [SerializeField] private string eraseSfxId;
        [SerializeField] private string failureSfxId;
        [SerializeField] private string successSfxId;
        [SerializeField] private AudioClip digitSfxOverride;
        [SerializeField] private AudioClip eraseSfxOverride;
        [SerializeField] private AudioClip failureSfxOverride;
        [SerializeField] private AudioClip successSfxOverride;
        [SerializeField] private AudioSource localAudioSource;

        [Header("Events")]
        [SerializeField] private UnityEvent onUnlocked;

        private bool isInRange;

        public override InteractMode Mode => InteractMode.Trigger;
        public bool IsUnlocked => unlocked;
        public int CodeLength => expectedCode.Length;

        protected override void Awake()
        {
            base.Awake();
            ApplyVisualState();
        }

        private void OnEnable()
        {
            ApplyVisualState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            expectedCode = KeepDigits(expectedCode);
            if (string.IsNullOrEmpty(expectedCode))
                expectedCode = "135689";

            ApplyVisualState();
        }
#endif

        public override bool CanInteract(PlayerInteractor interactor)
        {
            return !unlocked && base.CanInteract(interactor);
        }

        protected override bool ShouldShowInteractHint()
        {
            return !unlocked && base.ShouldShowInteractHint();
        }

        public override void SetInInteractRange(bool inRange)
        {
            base.SetInInteractRange(inRange);
            isInRange = inRange;
            ApplyVisualState();
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (unlocked)
                return;

            EventManager.Emit(PasswordLockEvents.OpenRequested, this);
        }

        /// <summary>
        /// Validates one complete attempt. Input buffering belongs to the UI,
        /// while the expected code and unlocked state remain gameplay-owned.
        /// </summary>
        public bool TryUnlock(string enteredCode)
        {
            if (unlocked)
                return true;

            bool isCorrect = string.Equals(
                enteredCode ?? string.Empty,
                expectedCode,
                StringComparison.Ordinal);

            if (!isCorrect)
            {
                PlayFailureFeedback();
                return false;
            }

            unlocked = true;
            isInRange = false;
            ApplyVisualState();
            PlayFeedback(successSfxOverride, successSfxId, GameSfxPaths.PuzzleSuccess);
            onUnlocked?.Invoke();
            EventManager.Emit(PasswordLockEvents.Unlocked, this);
            Debug.Log($"[PasswordLock] '{name}' unlocked successfully.", this);
            return true;
        }

        public void NotifyDigitPressed()
        {
            PlayFeedback(digitSfxOverride, digitSfxId, GameSfxPaths.UiConfirm);
        }

        public void NotifyErasePressed()
        {
            PlayFeedback(eraseSfxOverride, eraseSfxId, GameSfxPaths.UiBack);
        }

        public void ResetLock()
        {
            unlocked = false;
            isInRange = false;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (SpriteRenderer != null)
            {
                SpriteRenderer.color = unlocked
                    ? unlockedColor
                    : isInRange ? inRangeColor : lockedColor;
            }

            SetObjectsActive(hideOnUnlocked, !unlocked);
            SetObjectsActive(showOnUnlocked, unlocked);

            // Named fallbacks make the prototype prefab useful without Inspector wiring.
            SetNamedChildActive("LockedVisual", !unlocked);
            SetNamedChildActive("UnlockedContent", unlocked);
        }

        private void SetNamedChildActive(string childName, bool active)
        {
            Transform child = transform.Find(childName);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        private static void SetObjectsActive(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    objects[i].SetActive(active);
            }
        }

        private void PlayFeedback(AudioClip clipOverride, string resourceId, string fallbackResourcePath = null)
        {
            if (clipOverride != null)
            {
                if (localAudioSource == null)
                {
                    localAudioSource = GetComponent<AudioSource>();
                    if (localAudioSource == null)
                    {
                        localAudioSource = gameObject.AddComponent<AudioSource>();
                        localAudioSource.playOnAwake = false;
                    }
                }

                localAudioSource.PlayOneShot(clipOverride);
                return;
            }

            if (!string.IsNullOrWhiteSpace(resourceId) && GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlaySFXById(resourceId.Trim());
                return;
            }

            if (!string.IsNullOrWhiteSpace(fallbackResourcePath) && GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(fallbackResourcePath);
        }

        private void PlayFailureFeedback()
        {
            if (failureSfxOverride != null || !string.IsNullOrWhiteSpace(failureSfxId))
            {
                PlayFeedback(failureSfxOverride, failureSfxId);
                return;
            }

            if (GameAudioManager.Instance == null)
                return;

            GameAudioManager.Instance.PlayRandomSFXByResourcePaths(
                1f,
                GameSfxPaths.PuzzleFailure1,
                GameSfxPaths.PuzzleFailure3);
        }

        private static string KeepDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            StringBuilder builder = new(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsDigit(value[i]))
                    builder.Append(value[i]);
            }

            return builder.ToString();
        }
    }

    public static class PasswordLockEvents
    {
        public const string OpenRequested = "Gameplay.PasswordLock.OpenRequested";
        public const string Unlocked = "Gameplay.PasswordLock.Unlocked";
    }
}

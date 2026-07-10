using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 路灯（InteractableObjectB 变体）。
    /// 表世界开局熄灭，按 E 点亮本地小光源；同组顺序全部正确时播放提示音。
    /// 里世界仅作为位置标记，默认不可交互。
    /// </summary>
    public class InteractableObjectStreetLamp : InteractableObjectB
    {
        [Header("Street Lamp")]
        [Tooltip("亮灯时显示的小光源物体（默认找子物体 LightGlow）。")]
        [SerializeField] private GameObject lightGlowObject;
        [Tooltip("仅在表世界（World A）可交互；里世界只作标记。")]
        [SerializeField] private bool interactOnlyInOuterWorld = true;

        [Header("Sequence Success Audio")]
        [SerializeField] private AudioClip sequenceSuccessClip;
        [Tooltip("可选：走 GameAudioManager 的 SFX id；优先使用上面的 AudioClip。")]
        [SerializeField] private string sequenceSuccessSfxId;

        private bool wasActivated;
        private AudioSource localAudioSource;
        private static AudioClip fallbackBeepClip;

        protected override void Awake()
        {
            base.Awake();
            ResolveLightGlow();
            wasActivated = IsActivated;
            ApplyLightGlow();
        }

        private void LateUpdate()
        {
            if (wasActivated == IsActivated)
                return;

            wasActivated = IsActivated;
            ApplyLightGlow();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (interactOnlyInOuterWorld && IsInUnderworld())
                return false;

            return base.CanInteract(interactor);
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            base.OnInteract(interactor);
            wasActivated = IsActivated;
            ApplyLightGlow();

            if (IsActivated && AreAllLampsInSequenceGroupActivated())
                PlaySequenceSuccessAudio();
        }

        private bool AreAllLampsInSequenceGroupActivated()
        {
            string groupId = SequenceGroupId;
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            InteractableObjectStreetLamp[] lamps = FindObjectsOfType<InteractableObjectStreetLamp>();
            bool foundMember = false;
            for (int i = 0; i < lamps.Length; i++)
            {
                InteractableObjectStreetLamp lamp = lamps[i];
                if (lamp == null)
                    continue;

                if (!string.Equals(lamp.SequenceGroupId, groupId, System.StringComparison.Ordinal))
                    continue;

                foundMember = true;
                if (!lamp.IsActivated)
                    return false;
            }

            return foundMember;
        }

        private void ResolveLightGlow()
        {
            if (lightGlowObject != null)
                return;

            Transform child = transform.Find("LightGlow");
            if (child != null)
                lightGlowObject = child.gameObject;
        }

        private void ApplyLightGlow()
        {
            if (lightGlowObject == null)
                return;

            lightGlowObject.SetActive(IsActivated);
        }

        private void PlaySequenceSuccessAudio()
        {
            EnsureLocalAudioSource();

            if (sequenceSuccessClip != null)
            {
                localAudioSource.PlayOneShot(sequenceSuccessClip);
                return;
            }

            if (!string.IsNullOrWhiteSpace(sequenceSuccessSfxId) && GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlaySFXById(sequenceSuccessSfxId.Trim());
                return;
            }

            // Fallback cue when no clip / SFX id is assigned yet.
            localAudioSource.PlayOneShot(GetFallbackBeepClip());
        }

        private void EnsureLocalAudioSource()
        {
            if (localAudioSource != null)
                return;

            localAudioSource = GetComponent<AudioSource>();
            if (localAudioSource == null)
            {
                localAudioSource = gameObject.AddComponent<AudioSource>();
                localAudioSource.playOnAwake = false;
            }
        }

        private static AudioClip GetFallbackBeepClip()
        {
            if (fallbackBeepClip != null)
                return fallbackBeepClip;

            const int sampleRate = 44100;
            const float duration = 0.18f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - (t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * envelope * 0.35f;
            }

            fallbackBeepClip = AudioClip.Create("StreetLampSequenceBeep", sampleCount, 1, sampleRate, false);
            fallbackBeepClip.SetData(samples, 0);
            return fallbackBeepClip;
        }

        private static bool IsInUnderworld()
        {
            return GameManager.Instance != null && GameManager.Instance.IsInUnderworld;
        }
    }
}

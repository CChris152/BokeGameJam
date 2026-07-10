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

            if (ShouldPlaySequenceSuccessAudio())
                PlaySequenceSuccessAudio();
        }

        private bool ShouldPlaySequenceSuccessAudio()
        {
            if (string.IsNullOrWhiteSpace(SequenceGroupId))
                return false;

            string id = MechanismId;
            if (string.IsNullOrEmpty(id))
                return false;

            return IsActivated && IsMechanismSatisfied(id);
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
            if (sequenceSuccessClip != null)
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

                localAudioSource.PlayOneShot(sequenceSuccessClip);
                return;
            }

            if (!string.IsNullOrWhiteSpace(sequenceSuccessSfxId) && GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXById(sequenceSuccessSfxId.Trim());
        }

        private static bool IsInUnderworld()
        {
            return GameManager.Instance != null && GameManager.Instance.IsInUnderworld;
        }
    }
}

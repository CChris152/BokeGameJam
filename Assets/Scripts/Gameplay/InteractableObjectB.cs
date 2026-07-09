using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 可交互物体 B：触发型开关。按下后默认不可再互动；
    /// 若配置了序列组，则需按 sequenceIndex 顺序触发，全部触发后延迟复位。
    /// </summary>
    public class InteractableObjectB : InteractableObject
    {
        private static readonly Dictionary<string, List<InteractableObjectB>> groups = new();
        private static readonly HashSet<string> pendingResetGroups = new();
        private static readonly HashSet<string> satisfiedMechanisms = new();
        private static readonly List<InteractableObjectB> allActive = new();

        [Header("Sequence (optional)")]
        [Tooltip("留空 = 独立开关，触发后不再可互动。非空 = 同组按顺序触发，全部完成后复位。")]
        [SerializeField] private string sequenceGroupId;
        [SerializeField] private int sequenceIndex;
        [Tooltip("同组全部触发后，延迟多少秒再恢复原状。")]
        [SerializeField] private float resetDelaySeconds = 2f;

        [Header("Visual")]
        [SerializeField] private Sprite activatedSprite;
        [SerializeField] private Color activatedColor = new(0.5f, 0.5f, 0.5f, 1f);

        private Sprite idleSprite;
        private Color idleColor = Color.white;
        private bool activated;
        private Coroutine resetRoutine;

        public override InteractMode Mode => InteractMode.Trigger;
        public bool IsActivated => activated;

        /// <summary>
        /// 同 mechanismId 的 B 是否已成功触发（独立开关已激活，或序列组全部激活/等待复位中）。
        /// </summary>
        public static bool IsMechanismSatisfied(string mechanismId)
        {
            string id = NormalizeMechanismId(mechanismId);
            if (string.IsNullOrEmpty(id))
                return false;

            if (satisfiedMechanisms.Contains(id))
                return true;

            for (int i = 0; i < allActive.Count; i++)
            {
                InteractableObjectB item = allActive[i];
                if (item == null)
                    continue;

                if (!string.Equals(item.MechanismId, id, System.StringComparison.Ordinal))
                    continue;

                if (IsBSatisfied(item))
                    return true;
            }

            return false;
        }

        protected override void Awake()
        {
            base.Awake();

            if (SpriteRenderer != null)
            {
                idleSprite = SpriteRenderer.sprite;
                idleColor = SpriteRenderer.color;
            }
        }

        private void OnEnable()
        {
            if (!allActive.Contains(this))
                allActive.Add(this);

            RegisterToGroup();
        }

        private void OnDisable()
        {
            // Only the member hosting the delay coroutine should cancel it.
            if (resetRoutine != null)
            {
                StopCoroutine(resetRoutine);
                resetRoutine = null;
                pendingResetGroups.Remove(sequenceGroupId);
            }

            allActive.Remove(this);
            UnregisterFromGroup();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (activated)
                return false;

            if (!string.IsNullOrWhiteSpace(sequenceGroupId) && pendingResetGroups.Contains(sequenceGroupId))
                return false;

            return IsNextInSequence();
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            SetActivated(true);
            MarkMechanismSatisfiedIfReady();

            if (string.IsNullOrWhiteSpace(sequenceGroupId))
                return;

            if (AreAllActivatedInGroup())
                BeginGroupReset();
        }

        private bool IsNextInSequence()
        {
            if (string.IsNullOrWhiteSpace(sequenceGroupId))
                return true;

            if (!groups.TryGetValue(sequenceGroupId, out List<InteractableObjectB> members))
                return true;

            int nextIndex = int.MaxValue;
            for (int i = 0; i < members.Count; i++)
            {
                InteractableObjectB member = members[i];
                if (member == null || member.activated)
                    continue;

                if (member.sequenceIndex < nextIndex)
                    nextIndex = member.sequenceIndex;
            }

            return sequenceIndex == nextIndex;
        }

        private bool AreAllActivatedInGroup()
        {
            if (!groups.TryGetValue(sequenceGroupId, out List<InteractableObjectB> members))
                return false;

            for (int i = 0; i < members.Count; i++)
            {
                InteractableObjectB member = members[i];
                if (member != null && !member.activated)
                    return false;
            }

            return members.Count > 0;
        }

        private void BeginGroupReset()
        {
            if (pendingResetGroups.Contains(sequenceGroupId))
                return;

            pendingResetGroups.Add(sequenceGroupId);
            resetRoutine = StartCoroutine(ResetGroupAfterDelay());
        }

        private IEnumerator ResetGroupAfterDelay()
        {
            float delay = Mathf.Max(0f, resetDelaySeconds);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            ResetGroup();
            pendingResetGroups.Remove(sequenceGroupId);
            // Keep mechanism satisfied so delivery C stays open after a successful solve.
            resetRoutine = null;
        }

        private void ResetGroup()
        {
            if (!groups.TryGetValue(sequenceGroupId, out List<InteractableObjectB> members))
                return;

            for (int i = 0; i < members.Count; i++)
            {
                InteractableObjectB member = members[i];
                if (member != null)
                    member.SetActivated(false);
            }
        }

        private void MarkMechanismSatisfiedIfReady()
        {
            string id = MechanismId;
            if (string.IsNullOrEmpty(id))
                return;

            bool ready = false;
            if (!string.IsNullOrWhiteSpace(sequenceGroupId))
                ready = AreAllActivatedInGroup() || pendingResetGroups.Contains(sequenceGroupId);
            else
                ready = activated;

            if (!ready || satisfiedMechanisms.Contains(id))
                return;

            satisfiedMechanisms.Add(id);
            EventManager.Emit(GameEvents.MechanismSatisfied, id);
        }

        private static bool IsBSatisfied(InteractableObjectB item)
        {
            if (item.activated)
                return true;

            if (!string.IsNullOrWhiteSpace(item.sequenceGroupId)
                && pendingResetGroups.Contains(item.sequenceGroupId))
                return true;

            return false;
        }

        private static string NormalizeMechanismId(string mechanismId)
        {
            return string.IsNullOrWhiteSpace(mechanismId) ? null : mechanismId.Trim();
        }

        private void SetActivated(bool value)
        {
            activated = value;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (SpriteRenderer == null)
                return;

            if (activated)
            {
                if (activatedSprite != null)
                    SpriteRenderer.sprite = activatedSprite;
                SpriteRenderer.color = activatedColor;
            }
            else
            {
                SpriteRenderer.sprite = idleSprite;
                SpriteRenderer.color = idleColor;
            }
        }

        private void RegisterToGroup()
        {
            if (string.IsNullOrWhiteSpace(sequenceGroupId))
                return;

            if (!groups.TryGetValue(sequenceGroupId, out List<InteractableObjectB> members))
            {
                members = new List<InteractableObjectB>();
                groups[sequenceGroupId] = members;
            }

            if (!members.Contains(this))
                members.Add(this);
        }

        private void UnregisterFromGroup()
        {
            if (string.IsNullOrWhiteSpace(sequenceGroupId))
                return;

            if (!groups.TryGetValue(sequenceGroupId, out List<InteractableObjectB> members))
                return;

            members.Remove(this);
            if (members.Count == 0)
                groups.Remove(sequenceGroupId);
        }
    }
}

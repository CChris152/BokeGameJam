using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 鬼魂变体（继承 D）：空手时弹出对话；持有物品 A 时优先弹出互动选项
    ///（交给对方 / 只是聊聊）。交给对方会消耗持有物，并可标记 mechanism 已满足。
    /// </summary>
    public class InteractableObjectGhostChoice : InteractableObjectD
    {
        [Header("Choice (when holding item A)")]
        [SerializeField] private DialogueChoicePopup choicePanel;
        [SerializeField] private string choiceTitle = "要怎么做？";
        [SerializeField] private string giveOptionLabel = "交给对方";
        [SerializeField] private string talkOptionLabel = "只是聊聊";
        [TextArea(2, 6)]
        [SerializeField] private string giveSuccessDialogue = "谢谢你……";
        [Tooltip("mechanismId 非空时，仅同 id 的物品 A 会触发选项；为空则任意可拾取 A 均可。")]
        [SerializeField] private bool requireMatchingMechanismId = true;
        [Tooltip("成功交付后是否广播 MechanismSatisfied（供交付处 C 等使用）。")]
        [SerializeField] private bool emitMechanismSatisfiedOnGive = true;
        [Tooltip("交付成功后是否禁止再次交付（仍可对话）。")]
        [SerializeField] private bool giveOnlyOnce = true;

        private bool hasGiven;
        private PlayerInteractor pendingInteractor;

        protected override void Awake()
        {
            base.Awake();
            EnsureChoicePanel();
        }

        protected override void OnDisable()
        {
            if (choicePanel != null && DialogueChoicePopup.IsOpen)
                choicePanel.Close();

            base.OnDisable();
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            // Holding item A: options take priority over plain dialogue.
            if (HasUsableHeldItemA(interactor))
            {
                ShowChoiceOptions(interactor);
                return;
            }

            ShowDialogue(dialogueText);
        }

        private void ShowChoiceOptions(PlayerInteractor interactor)
        {
            EnsureChoicePanel();
            if (choicePanel == null)
            {
                Debug.LogError($"[InteractableObjectGhostChoice] '{name}' 缺少 ChoicePanel / DialogueChoicePopup。", this);
                ShowDialogue(dialogueText);
                return;
            }

            pendingInteractor = interactor;
            string itemName = interactor.HeldItem != null ? interactor.HeldItem.DisplayName : "物品";
            string primary = string.IsNullOrWhiteSpace(giveOptionLabel)
                ? $"交给对方（{itemName}）"
                : giveOptionLabel.Trim();

            choicePanel.Show(
                choiceTitle,
                primary,
                talkOptionLabel,
                OnGiveChosen,
                OnTalkChosen,
                transform);
        }

        private void OnGiveChosen()
        {
            PlayerInteractor interactor = pendingInteractor;
            pendingInteractor = null;

            if (interactor == null || !HasUsableHeldItemA(interactor))
            {
                ShowDialogue(dialogueText);
                return;
            }

            interactor.ConsumeHeldItem();
            hasGiven = true;

            if (emitMechanismSatisfiedOnGive && !string.IsNullOrEmpty(MechanismId))
                EventManager.Emit(GameEvents.MechanismSatisfied, MechanismId);

            ShowDialogue(giveSuccessDialogue);
        }

        private void OnTalkChosen()
        {
            pendingInteractor = null;
            ShowDialogue(dialogueText);
        }

        private bool HasUsableHeldItemA(PlayerInteractor interactor)
        {
            if (giveOnlyOnce && hasGiven)
                return false;

            if (interactor == null || !interactor.HasHeldItem)
                return false;

            InteractableObject held = interactor.HeldItem;
            if (held == null || held.Mode != InteractMode.PickUp)
                return false;

            if (!requireMatchingMechanismId)
                return true;

            if (string.IsNullOrEmpty(MechanismId))
                return true;

            return MatchesMechanism(held);
        }

        private void EnsureChoicePanel()
        {
            if (choicePanel != null)
                return;

            Transform child = transform.Find("ChoicePanel");
            if (child != null)
                choicePanel = child.GetComponent<DialogueChoicePopup>();

            if (choicePanel == null)
                choicePanel = GetComponentInChildren<DialogueChoicePopup>(true);
        }
    }
}

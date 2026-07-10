using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 鬼魂变体（继承 D）：空手时弹出对话；持有「糖」时优先弹出互动选项
    ///（交给对方 / 只是聊聊）。交给对方会消耗糖果并广播
    /// <see cref="GameEvents.CandyReceived"/>；本物体监听该事件后消失。
    /// </summary>
    public class InteractableObjectGhostChoice : InteractableObjectD
    {
        [Header("Choice (when holding candy)")]
        [SerializeField] private DialogueChoicePopup choicePanel;
        [SerializeField] private string choiceTitle = "要怎么做？";
        [SerializeField] private string giveOptionLabel = "交给对方";
        [SerializeField] private string talkOptionLabel = "只是聊聊";

        private PlayerInteractor pendingInteractor;

        protected override void Awake()
        {
            base.Awake();
            EnsureChoicePanel();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventManager.On(GameEvents.CandyReceived, OnCandyReceived);
        }

        protected override void OnDisable()
        {
            EventManager.Off(GameEvents.CandyReceived, OnCandyReceived);

            if (choicePanel != null && DialogueChoicePopup.IsOpen)
                choicePanel.Close();

            base.OnDisable();
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            NotifyInteracted();

            // 持有糖时优先弹出选项，否则普通对话。
            if (HasHeldCandy(interactor))
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
            string itemName = interactor.HeldItem != null ? interactor.HeldItem.DisplayName : "糖";
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

            if (interactor == null || !HasHeldCandy(interactor))
            {
                ShowDialogue(dialogueText);
                return;
            }

            interactor.ConsumeHeldItem();
            // 广播「拿到糖」；本物体通过监听该事件消失（也供其他系统订阅）。
            EventManager.Emit(GameEvents.CandyReceived);
        }

        private void OnTalkChosen()
        {
            pendingInteractor = null;
            ShowDialogue(dialogueText);
        }

        private void OnCandyReceived()
        {
            if (choicePanel != null && DialogueChoicePopup.IsOpen)
                choicePanel.Close();

            if (popupPanel != null && (popupPanel.isActiveAndEnabled || DialoguePopup.IsOpen))
                popupPanel.Close();
            else if (DialoguePopup.IsOpen)
                DialoguePopup.Hide();

            gameObject.SetActive(false);
        }

        private static bool HasHeldCandy(PlayerInteractor interactor)
        {
            if (interactor == null || !interactor.HasHeldItem)
                return false;

            InteractableObject held = interactor.HeldItem;
            if (held == null || held.Mode != InteractMode.PickUp)
                return false;

            return held is InteractableObjectPowerGated gated
                && gated.ItemKind == PowerGatedItemKind.Candy;
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

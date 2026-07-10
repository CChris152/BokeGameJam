using UnityEngine;
using BokeGameJam.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 可交互物体 D（鬼魂）：可反复互动，每关通常只有一个；
    /// 交互后显示自身 PopupPanel 子物体上的对话框。
    /// </summary>
    public class InteractableObjectD : InteractableObject
    {
        private static InteractableObjectD activeInLevel;

        [Header("Dialogue")]
        [TextArea(2, 8)]
        [SerializeField] protected string dialogueText = "……";
        [SerializeField] protected DialoguePopup popupPanel;

        public override InteractMode Mode => InteractMode.Trigger;
        public string DialogueText => dialogueText != null ? dialogueText : string.Empty;

        /// <summary>关卡编辑器写入对话正文。</summary>
        public void ApplyDialogueText(string text)
        {
            dialogueText = text ?? string.Empty;
        }

        protected override void Awake()
        {
            base.Awake();
            EnsurePopupPanel();
        }

        protected virtual void OnEnable()
        {
            if (activeInLevel != null && activeInLevel != this)
            {
                Debug.LogWarning(
                    $"[InteractableObjectD] 本关已有鬼魂 '{activeInLevel.name}'，又启用了 '{name}'。每关建议只放一个。",
                    this);
            }

            activeInLevel = this;
        }

        protected virtual void OnDisable()
        {
            if (activeInLevel == this)
                activeInLevel = null;

            if (popupPanel != null && DialoguePopup.IsOpen)
                popupPanel.Close();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            return !DialoguePopup.BlocksInteract;
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            ShowDialogue(dialogueText);
        }

        /// <summary>显示对话气泡；正文为空时用省略号。</summary>
        protected void ShowDialogue(string body)
        {
            EnsurePopupPanel();
            if (popupPanel == null)
            {
                Debug.LogError($"[InteractableObjectD] '{name}' 缺少 PopupPanel / DialoguePopup。", this);
                return;
            }

            string speaker = DisplayName;
            string text = string.IsNullOrWhiteSpace(body) ? "……" : body.Trim();
            popupPanel.Show(speaker, text, transform);
        }

        protected void EnsurePopupPanel()
        {
            if (popupPanel != null)
                return;

            Transform child = transform.Find("PopupPanel");
            if (child != null)
                popupPanel = child.GetComponent<DialoguePopup>();

            if (popupPanel == null)
                popupPanel = GetComponentInChildren<DialoguePopup>(true);
        }
    }
}

using System;
using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 可交互物体 D（鬼魂）：可反复互动，每关通常只有一个；
    /// 交互后显示自身 PopupPanel 子物体上的对话框。
    /// 切回表世界（A）时关闭对话；表世界中不可触发，回到里世界后仍可再触发。
    /// </summary>
    public class InteractableObjectD : InteractableObject
    {
        private static InteractableObjectD activeInLevel;

        /// <summary>任意鬼魂成功互动时触发（含 GhostChoice）。</summary>
        public static event Action Interacted;

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
            EventManager.On<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);
        }

        protected virtual void OnDisable()
        {
            EventManager.Off<WorldId>(GameEvents.ActiveWorldChanged, OnActiveWorldChanged);

            if (activeInLevel == this)
                activeInLevel = null;

            CloseDialogueIfOpen();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            // 表世界会关掉 B 层；物体禁用后仍可能留在 nearby，必须拒绝。
            // 不要做“回过表世界就永久禁用”，否则开局隐藏 B 层时会被误伤。
            return isActiveAndEnabled
                && gameObject.activeInHierarchy
                && !DialoguePopup.BlocksInteract;
        }

        public override void OnInteract(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;

            NotifyInteracted();
            ShowDialogue(dialogueText);
        }

        /// <summary>子类在成功进入互动流程时调用，供关卡剧情监听。</summary>
        protected void NotifyInteracted()
        {
            Interacted?.Invoke();
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

        private void OnActiveWorldChanged(WorldId world)
        {
            // 回到表世界：关掉已挂到 UI 根上的对话框，避免按 E 仍开关对话。
            if (world == WorldId.A)
                CloseDialogueIfOpen();
        }

        private void CloseDialogueIfOpen()
        {
            if (popupPanel != null)
            {
                if (popupPanel.isActiveAndEnabled || DialoguePopup.IsOpen)
                    popupPanel.Close();
                return;
            }

            if (DialoguePopup.IsOpen)
                DialoguePopup.Hide();
        }
    }
}

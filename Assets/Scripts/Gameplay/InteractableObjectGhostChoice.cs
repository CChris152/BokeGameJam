using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Data;
using BokeGameJam.UI;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 鬼魂变体（继承 D）：空手时弹出对话；持有「糖」时优先弹出互动选项
    ///（交给对方 / 只是聊聊）。交给对方会消耗糖果并广播
    /// <see cref="GameEvents.CandyReceived"/>；本物体监听该事件后消失。
    /// 持有非糖果拾取物交互视为交付失败；连续失败两次播放提示剧情。
    /// </summary>
    public class InteractableObjectGhostChoice : InteractableObjectD
    {
        private const string DefaultWrongDeliveryStoryPath = "ScriptableObjects/Stories/Story12";
        private const string DefaultCandySuccessStoryPath = "ScriptableObjects/Stories/Story13";
        private const int ConsecutiveFailsForHint = 2;

        [Header("Choice (when holding candy)")]
        [SerializeField] private DialogueChoicePopup choicePanel;
        [SerializeField] private string choiceTitle = "要怎么做？";
        [SerializeField] private string giveOptionLabel = "交给对方";
        [SerializeField] private string talkOptionLabel = "只是聊聊";

        [Header("Delivery Stories")]
        [Tooltip("连续交付失败达到次数后播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence wrongDeliveryStory;
        [SerializeField] private string wrongDeliveryStoryResourcePath = DefaultWrongDeliveryStoryPath;
        [Tooltip("交付糖果成功时播放；留空则按 Resources 路径加载。")]
        [SerializeField] private StorySequence candySuccessStory;
        [SerializeField] private string candySuccessStoryResourcePath = DefaultCandySuccessStoryPath;

        private PlayerInteractor pendingInteractor;
        private int consecutiveFailedDeliveries;

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

            // 持有糖时优先弹出选项。
            if (HasHeldCandy(interactor))
            {
                ShowChoiceOptions(interactor);
                RefreshInteractHint();
                return;
            }

            // 持有其他可拾取物：视为交付失败。
            if (HasHeldNonCandyPickup(interactor))
            {
                RegisterFailedDelivery();
                return;
            }

            ShowDialogue(dialogueText);
            RefreshInteractHint();
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

            consecutiveFailedDeliveries = 0;
            interactor.ConsumeHeldItem();

            if (GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlaySFXByResourcePath(GameSfxPaths.ClockHourBell);

            PlayBannerStory(ResolveCandySuccessStory(), "交付糖果成功剧情");
            // 广播「拿到糖」；本物体通过监听该事件消失（也供其他系统订阅）。
            EventManager.Emit(GameEvents.CandyReceived);
        }

        private void OnTalkChosen()
        {
            pendingInteractor = null;
            ShowDialogue(dialogueText);
        }

        private void RegisterFailedDelivery()
        {
            consecutiveFailedDeliveries++;
            if (consecutiveFailedDeliveries < ConsecutiveFailsForHint)
            {
                ShowDialogue("不是这个！");
                return;
            }

            consecutiveFailedDeliveries = 0;
            PlayBannerStory(ResolveWrongDeliveryStory(), "连续交付失败剧情");
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

        private static bool HasHeldNonCandyPickup(PlayerInteractor interactor)
        {
            if (interactor == null || !interactor.HasHeldItem)
                return false;

            InteractableObject held = interactor.HeldItem;
            return held != null
                && held.Mode == InteractMode.PickUp
                && !HasHeldCandy(interactor);
        }

        private StorySequence ResolveWrongDeliveryStory()
        {
            return ResolveStory(wrongDeliveryStory, wrongDeliveryStoryResourcePath);
        }

        private StorySequence ResolveCandySuccessStory()
        {
            return ResolveStory(candySuccessStory, candySuccessStoryResourcePath);
        }

        private static StorySequence ResolveStory(StorySequence assigned, string resourcePath)
        {
            if (assigned != null)
                return assigned;

            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            return Resources.Load<StorySequence>(resourcePath.Trim());
        }

        private void PlayBannerStory(StorySequence story, string storyLabel)
        {
            if (story == null || !story.HasLines)
            {
                Debug.LogWarning($"[InteractableObjectGhostChoice] {storyLabel}配置缺失或为空。", this);
                return;
            }

            CameraTopBannerUI banner = EnsureTopBanner();
            if (banner == null)
            {
                Debug.LogWarning(
                    $"[InteractableObjectGhostChoice] CameraTopBannerUI 未找到，无法播放{storyLabel}。",
                    this);
                return;
            }

            banner.PlayStory(story.CreateLineList());
        }

        private static CameraTopBannerUI EnsureTopBanner()
        {
            CameraTopBannerUI banner = CameraTopBannerUI.Instance;
            if (banner != null)
                return banner;

            if (UIManager.Instance == null)
                return null;

            GameObject go = UIManager.Instance.Load(CameraTopBannerUI.ResourceId);
            return go != null ? go.GetComponent<CameraTopBannerUI>() : null;
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

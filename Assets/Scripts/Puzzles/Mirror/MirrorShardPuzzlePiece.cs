using UnityEngine;
using UnityEngine.EventSystems;

namespace BokeGameJam.Puzzles.Mirror
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MirrorShardPuzzlePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Target")]
        [SerializeField] private RectTransform target;
        [SerializeField] private float snapDistance = 36f;
        [SerializeField] private bool lockWhenPlaced = true;

        private RectTransform rectTransform;
        private RectTransform parentRect;
        private Canvas parentCanvas;
        private Vector2 pointerOffset;
        private Vector2 startAnchoredPosition;
        private bool hasStartPose;
        private bool isDragging;
        private bool isPlaced;

        public MirrorShardPuzzlePanel Owner { get; set; }
        public bool IsPlaced => isPlaced;

        private void Awake()
        {
            ResolveReferences();
            CaptureStartPose();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CaptureStartPose();
        }

        public void ResetPiece()
        {
            ResolveReferences();
            CaptureStartPose();

            isPlaced = false;
            isDragging = false;
            rectTransform.anchoredPosition = startAnchoredPosition;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isPlaced && lockWhenPlaced)
                return;

            ResolveReferences();
            if (parentRect == null)
                return;

            isDragging = true;
            rectTransform.SetAsLastSibling();

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out Vector2 localPointer))
                pointerOffset = rectTransform.anchoredPosition - localPointer;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || (isPlaced && lockWhenPlaced) || parentRect == null)
                return;

            Camera eventCamera = eventData.pressEventCamera;
            if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                eventCamera = null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventCamera, out Vector2 localPointer))
            {
                isPlaced = false;
                rectTransform.anchoredPosition = localPointer + pointerOffset;
                Owner?.NotifyPieceChanged();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging)
                return;

            isDragging = false;
            TrySnap();
            Owner?.NotifyPieceChanged();
        }

        public void TrySnap()
        {
            ResolveReferences();

            if (target == null || parentRect == null)
            {
                isPlaced = false;
                return;
            }

            Vector2 targetPosition = GetTargetPositionInParent();
            float distance = Vector2.Distance(rectTransform.anchoredPosition, targetPosition);

            if (distance > snapDistance)
            {
                isPlaced = false;
                return;
            }

            rectTransform.anchoredPosition = targetPosition;
            isPlaced = true;
        }

        private Vector2 GetTargetPositionInParent()
        {
            if (target.parent == rectTransform.parent)
                return target.anchoredPosition;

            Camera camera = null;
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                camera = parentCanvas.worldCamera;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, camera, out Vector2 localPoint);
            return localPoint;
        }

        private void ResolveReferences()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (parentRect == null)
                parentRect = rectTransform.parent as RectTransform;

            if (parentCanvas == null)
                parentCanvas = GetComponentInParent<Canvas>();
        }

        private void CaptureStartPose()
        {
            if (hasStartPose || rectTransform == null)
                return;

            startAnchoredPosition = rectTransform.anchoredPosition;
            hasStartPose = true;
        }
    }
}

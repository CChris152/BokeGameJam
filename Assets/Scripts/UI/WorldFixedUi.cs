using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 将 Canvas 固定在世界坐标：World Space、脱离相机/UIRoot，不随镜头移动。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorldFixedUi : MonoBehaviour
    {
        [SerializeField] private bool unparentOnAwake = true;
        [SerializeField] private bool lockWorldPose = true;
        [SerializeField] private int sortingOrder = 50;

        private Canvas canvas;
        private Vector3 lockedPosition;
        private Quaternion lockedRotation;
        private Vector3 lockedScale;
        private bool hasLockedPose;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            ApplyWorldSpaceSettings();

            if (unparentOnAwake && transform.parent != null)
                transform.SetParent(null, true);

            CapturePose();
        }

        private void OnEnable()
        {
            ApplyWorldSpaceSettings();
            if (!hasLockedPose)
                CapturePose();
        }

        private void LateUpdate()
        {
            if (!lockWorldPose || !hasLockedPose)
                return;

            if (transform.parent != null)
                transform.SetParent(null, true);

            transform.SetPositionAndRotation(lockedPosition, lockedRotation);
            transform.localScale = lockedScale;
        }

        /// <summary>按当前世界姿态重新锁定（场景摆放后可调用）。</summary>
        public void RecapturePose()
        {
            CapturePose();
        }

        private void ApplyWorldSpaceSettings()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();
            if (canvas == null)
                return;

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            if (canvas.worldCamera == null && Camera.main != null)
                canvas.worldCamera = Camera.main;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.dynamicPixelsPerUnit = 1f;
            }
        }

        private void CapturePose()
        {
            lockedPosition = transform.position;
            lockedRotation = transform.rotation;
            lockedScale = transform.localScale;
            hasLockedPose = true;
        }
    }
}

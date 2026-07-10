using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 给 UI Image 做「左小右大」的透视变形。
    /// 挂在带 <see cref="Graphic"/>（如 Image）的物体上，在 Inspector 里调 leftScale / rightScale 即可。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class UIImagePerspective : BaseMeshEffect
    {
        [Header("Scale (Left → Right)")]
        [Tooltip("左侧缩放（越小越远）")]
        [Range(0.2f, 2f)]
        [SerializeField] private float leftScale = 0.72f;

        [Tooltip("右侧缩放（越大越近）")]
        [Range(0.2f, 2f)]
        [SerializeField] private float rightScale = 1.28f;

        [Header("Extra Perspective")]
        [Tooltip("垂直错切：右侧相对左侧的上下偏移（正值=右侧上移）")]
        [SerializeField] private float verticalShear;

        [Tooltip("缩放中心：0=底部，0.5=中心，1=顶部")]
        [Range(0f, 1f)]
        [SerializeField] private float scalePivotY = 0.5f;

        [Tooltip("是否在编辑器非播放时也预览效果")]
        [SerializeField] private bool previewInEditMode = true;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            leftScale = Mathf.Max(0.05f, leftScale);
            rightScale = Mathf.Max(0.05f, rightScale);
            base.OnValidate();
        }
#endif

        public override void ModifyMesh(VertexHelper vh)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !previewInEditMode)
                return;
#endif
            if (vh == null || vh.currentVertCount == 0)
                return;

            UIVertex vertex = default;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector3 p = vertex.position;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            if (Mathf.Approximately(maxX, minX))
                return;

            float width = maxX - minX;
            float height = Mathf.Max(0.0001f, maxY - minY);
            float pivotY = Mathf.Lerp(minY, maxY, scalePivotY);
            float centerX = (minX + maxX) * 0.5f;

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector3 p = vertex.position;
                float t = Mathf.Clamp01((p.x - minX) / width);
                float scale = Mathf.Lerp(leftScale, rightScale, t);
                float shear = verticalShear * t;

                p.y = pivotY + (p.y - pivotY) * scale + shear;
                p.x = centerX + (p.x - centerX) * Mathf.Lerp(1f, scale, 0.35f);
                vertex.position = p;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}

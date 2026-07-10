using TMPro;
using UnityEngine;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 给 TMP 标题做「左小右大」的透视变形。
    /// 挂在带 TextMeshProUGUI 的物体上，在 Inspector 里调 leftScale / rightScale 即可。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class UITextPerspective : MonoBehaviour
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

        private TMP_Text text;
        private bool isDirty = true;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (text == null)
                text = GetComponent<TMP_Text>();

            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            isDirty = true;
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            RestoreMesh();
        }

        private void OnValidate()
        {
            leftScale = Mathf.Max(0.05f, leftScale);
            rightScale = Mathf.Max(0.05f, rightScale);
            isDirty = true;
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !previewInEditMode)
                return;
#endif
            if (text == null)
                return;

            if (isDirty || text.havePropertiesChanged)
                ApplyPerspective();
        }

        private void OnTextChanged(Object obj)
        {
            if (obj == text)
                isDirty = true;
        }

        /// <summary>按左右缩放与错切，变形当前 TMP 网格。</summary>
        private void ApplyPerspective()
        {
            text.ForceMeshUpdate(ignoreActiveState: true);
            TMP_TextInfo textInfo = text.textInfo;
            if (textInfo == null || textInfo.characterCount == 0)
            {
                isDirty = false;
                return;
            }

            // 先取整段文字包围盒，用于把每个顶点的 X 归一化到 0~1
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            bool hasVisible = false;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;

                hasVisible = true;
                minX = Mathf.Min(minX, charInfo.bottomLeft.x, charInfo.topLeft.x);
                maxX = Mathf.Max(maxX, charInfo.bottomRight.x, charInfo.topRight.x);
                minY = Mathf.Min(minY, charInfo.bottomLeft.y, charInfo.bottomRight.y);
                maxY = Mathf.Max(maxY, charInfo.topLeft.y, charInfo.topRight.y);
            }

            if (!hasVisible || Mathf.Approximately(maxX, minX))
            {
                isDirty = false;
                return;
            }

            float width = maxX - minX;
            float height = Mathf.Max(0.0001f, maxY - minY);
            float pivotY = Mathf.Lerp(minY, maxY, scalePivotY);

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                for (int v = 0; v < 4; v++)
                {
                    Vector3 vertex = vertices[vertexIndex + v];
                    float t = Mathf.Clamp01((vertex.x - minX) / width);
                    float scale = Mathf.Lerp(leftScale, rightScale, t);
                    float shear = verticalShear * t;

                    vertex.y = pivotY + (vertex.y - pivotY) * scale + shear;
                    // 轻微水平拉开，避免只压扁高度显得挤
                    float centerX = (minX + maxX) * 0.5f;
                    vertex.x = centerX + (vertex.x - centerX) * Mathf.Lerp(1f, scale, 0.35f);

                    vertices[vertexIndex + v] = vertex;
                }
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                text.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }

            isDirty = false;
        }

        private void RestoreMesh()
        {
            if (text == null)
                return;

            text.ForceMeshUpdate(ignoreActiveState: true);
        }
    }
}

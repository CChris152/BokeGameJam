using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Data
{
    /// <summary>
    /// 剧情字幕配置：一组按顺序播放的文本行。
    /// </summary>
    [CreateAssetMenu(fileName = "StorySequence", menuName = "BokeGameJam/Story Sequence", order = 110)]
    public sealed class StorySequence : ScriptableObject
    {
        [Tooltip("剧情唯一 id，便于代码查找。")]
        [SerializeField] private string storyId = "untitled";

        [Tooltip("按顺序显示的字幕行。")]
        [SerializeField] private List<string> lines = new();

        public string StoryId => storyId != null ? storyId.Trim() : string.Empty;

        public IReadOnlyList<string> Lines => lines;

        public int Count => lines != null ? lines.Count : 0;

        public bool HasLines => Count > 0;

        /// <summary>复制一份可修改的行列表，供播放器使用。</summary>
        public List<string> CreateLineList()
        {
            List<string> copy = new(Count);
            if (lines == null)
                return copy;

            for (int i = 0; i < lines.Count; i++)
                copy.Add(lines[i] ?? string.Empty);

            return copy;
        }
    }
}

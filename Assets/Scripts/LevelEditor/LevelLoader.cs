using System.IO;
using UnityEngine;

namespace BokeGameJam.LevelEditor
{
    /// <summary>
    /// 关卡"默认地图"回退加载器（可选）。
    ///
    /// 命名规范：<see cref="LevelEditor"/> 会按【场景名】自动读取 persistentDataPath 中的地图。
    /// 因此本组件只在【场景中还没有存档】时兜底一次，从 Resources 里拖出一份默认地图。
    ///
    /// 用法：
    ///   • 一个场景一张地图：无需挂载 LevelLoader，LevelEditor 自动读同名文件
    ///   • 想在没有存档时提供一个初始地图：挂 LevelLoader + 拖 TextAsset 进 defaultLevel
    /// </summary>
    [RequireComponent(typeof(LevelEditor))]
    public sealed class LevelLoader : MonoBehaviour
    {
        [Tooltip("兜底用：如果 persistentDataPath 中不存在同名地图，就加载这份 Resources 里的 JSON")]
        [SerializeField] private TextAsset defaultLevel;

        private LevelEditor editor;

        private void Awake()
        {
            editor = GetComponent<LevelEditor>();
        }

        private void Start()
        {
            // 存档已存在 → LevelEditor.Start() 里的 LoadSilent() 已经处理，不再重复
            if (File.Exists(editor.SaveFilePath))
                return;

            if (defaultLevel == null)
                return;

            try
            {
                LevelData data = LevelData.FromJson(defaultLevel.text);
                editor.ApplyLevelData(data);
                Debug.Log($"[LevelLoader] 已从 Resources 加载默认地图: {defaultLevel.name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LevelLoader] 默认关卡解析失败: {ex.Message}");
            }
        }
    }
}

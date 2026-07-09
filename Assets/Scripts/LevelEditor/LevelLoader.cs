using System.IO;
using UnityEngine;

namespace BokeGameJam.LevelEditor
{
    /// <summary>
    /// 启动时自动加载关卡文件。可挂在与 LevelEditor 相同的对象上。
    /// 加载顺序：
    ///   1) 若 externalPath（persistentDataPath 下）存在，优先加载
    ///   2) 否则加载 Resources 下的默认关卡 TextAsset
    /// </summary>
    [RequireComponent(typeof(LevelEditor))]
    public sealed class LevelLoader : MonoBehaviour
    {
        [Tooltip("从 Resources 加载的默认关卡 JSON（无扩展名路径）")]
        [SerializeField] private TextAsset defaultLevel;

        [Tooltip("启动时是否加载 persistentDataPath 中的存档")]
        [SerializeField] private bool loadPersistentOnStart = true;

        [Tooltip("启动时是否退出编辑模式")]
        [SerializeField] private bool disableEditModeOnLoad;

        private LevelEditor editor;

        private void Awake()
        {
            editor = GetComponent<LevelEditor>();
        }

        private void Start()
        {
            LevelData data = null;

            if (loadPersistentOnStart && File.Exists(editor.SaveFilePath))
            {
                try
                {
                    data = LevelData.FromJson(File.ReadAllText(editor.SaveFilePath));
                    Debug.Log($"[LevelLoader] 已从存档加载: {editor.SaveFilePath}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LevelLoader] 读取存档失败: {ex.Message}");
                }
            }

            if ((data == null || data.tiles.Count == 0) && defaultLevel != null)
            {
                data = LevelData.FromJson(defaultLevel.text);
                Debug.Log($"[LevelLoader] 已加载默认关卡: {defaultLevel.name}");
            }

            if (data != null)
                editor.ApplyLevelData(data);
        }
    }
}

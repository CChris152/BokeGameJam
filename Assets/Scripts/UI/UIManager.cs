using System.Collections.Generic;
using BokeGameJam.Core;
using UnityEngine;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 运行时 UI 管理器。按 ResourceDefinitionDatabase 的 resourceId 加载/关闭 UI。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI Root")]
        [Tooltip("在预制体上绑定 Canvas / UIRoot；未绑定则 Load 会失败。")]
        [SerializeField] private Transform uiRoot;

        [Tooltip("是否跨场景保留本管理器。")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        /// <summary>已加载 UI 实例缓存：key 为资源 id，value 为场景中的实例。</summary>
        private readonly Dictionary<string, GameObject> loadedUIs = new();

        public Transform UIRoot => uiRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            if (uiRoot == null)
                Debug.LogError("[UIManager] uiRoot 未绑定，请在 UIManager 预制体上指定 Canvas/UIRoot。", this);
        }

        /// <summary>按 resourceId 加载并显示 UI；已加载则重新激活并置顶。</summary>
        public GameObject Load(string resourceId)
        {
            string id = NormalizeId(resourceId);
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("[UIManager] resourceId is empty.", this);
                return null;
            }

            if (loadedUIs.TryGetValue(id, out GameObject cachedUI))
            {
                if (cachedUI == null)
                {
                    loadedUIs.Remove(id);
                }
                else
                {
                    cachedUI.SetActive(true);
                    cachedUI.transform.SetAsLastSibling();
                    return cachedUI;
                }
            }

            if (uiRoot == null)
            {
                Debug.LogError($"[UIManager] Cannot load '{id}': uiRoot is not assigned.", this);
                return null;
            }

            if (!ResourcesManager.TryGetUI(id, out ResourceDefinitionDatabase.UIResource uiResource))
            {
                Debug.LogError($"[UIManager] Cannot find UI resource id: {id}", this);
                return null;
            }

            GameObject prefab = ResourcesManager.LoadUI(uiResource);
            if (prefab == null)
                return null;

            GameObject uiInstance = Instantiate(prefab, uiRoot, false);
            uiInstance.name = id;
            uiInstance.SetActive(true);
            loadedUIs[id] = uiInstance;
            return uiInstance;
        }

        /// <summary>销毁已加载的 UI 实例。</summary>
        public bool Close(string resourceId)
        {
            string id = NormalizeId(resourceId);
            if (string.IsNullOrEmpty(id))
                return false;

            if (!loadedUIs.TryGetValue(id, out GameObject ui))
                return false;

            loadedUIs.Remove(id);

            if (ui != null)
                Destroy(ui);

            return true;
        }

        /// <summary>已加载且当前激活则为 true。</summary>
        public bool IsVisible(string resourceId)
        {
            string id = NormalizeId(resourceId);
            if (string.IsNullOrEmpty(id))
                return false;

            return loadedUIs.TryGetValue(id, out GameObject ui) && ui != null && ui.activeSelf;
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }
    }
}

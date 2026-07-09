using System.Collections.Generic;
using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// 运行时 UI 管理器（单例）。
    /// 负责按资源库 id 加载/隐藏/关闭 UI 预制体实例；预制体引用由 ResourcesManager 解析。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI 根节点")]
        [Tooltip("所有动态 UI 的父节点。为空时会自动创建带 Canvas 的 UIRoot。")]
        [SerializeField] private Transform uiRoot;

        [Tooltip("是否跨场景保留本管理器。")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        /// <summary>已加载 UI 实例缓存：key 为资源 id，value 为场景中的实例。</summary>
        private readonly Dictionary<string, GameObject> loadedUIs = new();

        /// <summary>UI 根节点；首次访问时若缺失会自动创建。</summary>
        public Transform UIRoot
        {
            get
            {
                EnsureUIRoot();
                return uiRoot;
            }
        }

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

            EnsureUIRoot();
        }

        /// <summary>
        /// 按需加载 UI 预制体并显示（挂到默认 UIRoot 下）。
        /// </summary>
        public GameObject LoadUI(ResourceDefinitionDatabase.UIResource uiResource)
        {
            return LoadUI(uiResource, UIRoot);
        }

        /// <summary>
        /// 按需在指定父节点下加载 UI 预制体并显示。
        /// 若同 id 已加载则直接激活并置顶，不重复实例化。
        /// </summary>
        public GameObject LoadUI(ResourceDefinitionDatabase.UIResource uiResource, Transform parent)
        {
            string cacheKey = GetUIId(uiResource);
            if (string.IsNullOrEmpty(cacheKey))
                return null;

            if (loadedUIs.TryGetValue(cacheKey, out GameObject cachedUI))
            {
                if (cachedUI == null)
                {
                    // 实例已被外部销毁，清理脏缓存
                    loadedUIs.Remove(cacheKey);
                }
                else
                {
                    cachedUI.SetActive(true);
                    cachedUI.transform.SetAsLastSibling();
                    return cachedUI;
                }
            }

            GameObject prefab = ResourcesManager.LoadUI(uiResource);
            if (prefab == null)
                return null;

            Transform targetParent = parent != null ? parent : UIRoot;
            GameObject uiInstance = Instantiate(prefab, targetParent, false);
            uiInstance.name = cacheKey;
            uiInstance.SetActive(true);
            loadedUIs[cacheKey] = uiInstance;

            return uiInstance;
        }

        /// <summary>按资源 id 加载并显示 UI。</summary>
        public GameObject LoadUIById(string uiId)
        {
            return LoadUIById(uiId, UIRoot);
        }

        /// <summary>按资源 id 在指定父节点下加载并显示 UI。</summary>
        public GameObject LoadUIById(string uiId, Transform parent)
        {
            if (!ResourcesManager.TryGetUI(uiId, out ResourceDefinitionDatabase.UIResource uiResource))
            {
                Debug.LogError($"[UIManager] Cannot find UI resource id: {uiId}");
                return null;
            }

            return LoadUI(uiResource, parent);
        }

        /// <summary>加载 UI 并返回其上的指定组件。</summary>
        public T LoadUI<T>(ResourceDefinitionDatabase.UIResource uiResource) where T : Component
        {
            GameObject ui = LoadUI(uiResource);
            return ui != null ? ui.GetComponent<T>() : null;
        }

        /// <summary>按 id 加载 UI 并返回其上的指定组件。</summary>
        public T LoadUIById<T>(string uiId) where T : Component
        {
            GameObject ui = LoadUIById(uiId);
            return ui != null ? ui.GetComponent<T>() : null;
        }

        /// <summary>隐藏 UI（不销毁，可再次 Load/显示）。</summary>
        public bool HideUI(ResourceDefinitionDatabase.UIResource uiResource)
        {
            if (!TryGetLoadedUI(uiResource, out GameObject ui))
                return false;

            ui.SetActive(false);
            return true;
        }

        /// <summary>按 id 隐藏 UI（不销毁）。</summary>
        public bool HideUIById(string uiId)
        {
            if (!TryGetLoadedUIById(uiId, out GameObject ui))
                return false;

            ui.SetActive(false);
            return true;
        }

        /// <summary>关闭并销毁 UI 实例，同时从缓存移除。</summary>
        public bool CloseUI(ResourceDefinitionDatabase.UIResource uiResource)
        {
            string cacheKey = GetUIId(uiResource);
            return CloseUIByCacheKey(cacheKey);
        }

        /// <summary>按 id 关闭并销毁 UI 实例。</summary>
        public bool CloseUIById(string uiId)
        {
            return CloseUIByCacheKey(NormalizeId(uiId));
        }

        /// <summary>隐藏当前所有已加载 UI（不销毁）。</summary>
        public void HideAllUI()
        {
            foreach (GameObject ui in loadedUIs.Values)
            {
                if (ui != null)
                    ui.SetActive(false);
            }
        }

        /// <summary>关闭并销毁当前所有已加载 UI。</summary>
        public void CloseAllUI()
        {
            foreach (GameObject ui in loadedUIs.Values)
            {
                if (ui != null)
                    Destroy(ui);
            }

            loadedUIs.Clear();
        }

        /// <summary>尝试获取已加载的 UI 实例。</summary>
        public bool TryGetLoadedUI(ResourceDefinitionDatabase.UIResource uiResource, out GameObject ui)
        {
            string cacheKey = GetUIId(uiResource);
            return TryGetLoadedUIByCacheKey(cacheKey, out ui);
        }

        /// <summary>按 id 尝试获取已加载的 UI 实例。</summary>
        public bool TryGetLoadedUIById(string uiId, out GameObject ui)
        {
            return TryGetLoadedUIByCacheKey(NormalizeId(uiId), out ui);
        }

        /// <summary>获取已加载 UI 上的指定组件。</summary>
        public T GetLoadedUI<T>(ResourceDefinitionDatabase.UIResource uiResource) where T : Component
        {
            return TryGetLoadedUI(uiResource, out GameObject ui) ? ui.GetComponent<T>() : null;
        }

        /// <summary>按 id 获取已加载 UI 上的指定组件。</summary>
        public T GetLoadedUIById<T>(string uiId) where T : Component
        {
            return TryGetLoadedUIById(uiId, out GameObject ui) ? ui.GetComponent<T>() : null;
        }

        /// <summary>判断指定 UI 是否已加载且当前可见。</summary>
        public bool IsUIVisible(ResourceDefinitionDatabase.UIResource uiResource)
        {
            return TryGetLoadedUI(uiResource, out GameObject ui) && ui.activeSelf;
        }

        /// <summary>按 id 判断指定 UI 是否已加载且当前可见。</summary>
        public bool IsUIVisibleById(string uiId)
        {
            return TryGetLoadedUIById(uiId, out GameObject ui) && ui.activeSelf;
        }

        /// <summary>
        /// 确保存在 UIRoot（含 Canvas / CanvasScaler / GraphicRaycaster）。
        /// sortingOrder 设为 100，保证弹窗盖在场景内普通 UI 之上。
        /// </summary>
        private void EnsureUIRoot()
        {
            if (uiRoot != null)
                return;

            GameObject root = new GameObject("UIRoot");
            root.transform.SetParent(transform, false);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler canvasScaler = root.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            uiRoot = root.transform;
        }

        /// <summary>按缓存 key 关闭并销毁 UI。</summary>
        private bool CloseUIByCacheKey(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey))
                return false;

            if (!loadedUIs.TryGetValue(cacheKey, out GameObject ui))
                return false;

            loadedUIs.Remove(cacheKey);

            if (ui != null)
                Destroy(ui);

            return true;
        }

        /// <summary>按缓存 key 获取已加载实例；若引用已失效则清理缓存。</summary>
        private bool TryGetLoadedUIByCacheKey(string cacheKey, out GameObject ui)
        {
            if (string.IsNullOrEmpty(cacheKey))
            {
                ui = null;
                return false;
            }

            if (!loadedUIs.TryGetValue(cacheKey, out ui))
                return false;

            if (ui != null)
                return true;

            loadedUIs.Remove(cacheKey);
            return false;
        }

        /// <summary>从 UI 资源条目提取规范化 id。</summary>
        private string GetUIId(ResourceDefinitionDatabase.UIResource uiResource)
        {
            if (uiResource == null)
            {
                Debug.LogWarning("[UIManager] UI resource is null.");
                return null;
            }

            string id = NormalizeId(uiResource.Id);
            if (!string.IsNullOrEmpty(id))
                return id;

            Debug.LogWarning("[UIManager] UI resource id is empty.");
            return null;
        }

        /// <summary>去除空白；空字符串返回 null。</summary>
        private string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }
    }
}

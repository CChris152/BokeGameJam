using System.Collections.Generic;
using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Runtime UI manager. UI prefab references are resolved through ResourcesManager.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI Root")]
        [SerializeField] private Transform uiRoot;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly Dictionary<string, GameObject> loadedUIs = new();

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
        /// Loads a UI prefab if needed, then shows it.
        /// </summary>
        public GameObject LoadUI(ResourceDefinitionDatabase.UIResource uiResource)
        {
            return LoadUI(uiResource, UIRoot);
        }

        /// <summary>
        /// Loads a UI prefab under the given parent if needed, then shows it.
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

        public GameObject LoadUIById(string uiId)
        {
            return LoadUIById(uiId, UIRoot);
        }

        public GameObject LoadUIById(string uiId, Transform parent)
        {
            if (!ResourcesManager.TryGetUI(uiId, out ResourceDefinitionDatabase.UIResource uiResource))
            {
                Debug.LogError($"[UIManager] Cannot find UI resource id: {uiId}");
                return null;
            }

            return LoadUI(uiResource, parent);
        }

        public T LoadUI<T>(ResourceDefinitionDatabase.UIResource uiResource) where T : Component
        {
            GameObject ui = LoadUI(uiResource);
            return ui != null ? ui.GetComponent<T>() : null;
        }

        public T LoadUIById<T>(string uiId) where T : Component
        {
            GameObject ui = LoadUIById(uiId);
            return ui != null ? ui.GetComponent<T>() : null;
        }

        public bool HideUI(ResourceDefinitionDatabase.UIResource uiResource)
        {
            if (!TryGetLoadedUI(uiResource, out GameObject ui))
                return false;

            ui.SetActive(false);
            return true;
        }

        public bool HideUIById(string uiId)
        {
            if (!TryGetLoadedUIById(uiId, out GameObject ui))
                return false;

            ui.SetActive(false);
            return true;
        }

        public bool CloseUI(ResourceDefinitionDatabase.UIResource uiResource)
        {
            string cacheKey = GetUIId(uiResource);
            return CloseUIByCacheKey(cacheKey);
        }

        public bool CloseUIById(string uiId)
        {
            return CloseUIByCacheKey(NormalizeId(uiId));
        }

        public void HideAllUI()
        {
            foreach (GameObject ui in loadedUIs.Values)
            {
                if (ui != null)
                    ui.SetActive(false);
            }
        }

        public void CloseAllUI()
        {
            foreach (GameObject ui in loadedUIs.Values)
            {
                if (ui != null)
                    Destroy(ui);
            }

            loadedUIs.Clear();
        }

        public bool TryGetLoadedUI(ResourceDefinitionDatabase.UIResource uiResource, out GameObject ui)
        {
            string cacheKey = GetUIId(uiResource);
            return TryGetLoadedUIByCacheKey(cacheKey, out ui);
        }

        public bool TryGetLoadedUIById(string uiId, out GameObject ui)
        {
            return TryGetLoadedUIByCacheKey(NormalizeId(uiId), out ui);
        }

        public T GetLoadedUI<T>(ResourceDefinitionDatabase.UIResource uiResource) where T : Component
        {
            return TryGetLoadedUI(uiResource, out GameObject ui) ? ui.GetComponent<T>() : null;
        }

        public T GetLoadedUIById<T>(string uiId) where T : Component
        {
            return TryGetLoadedUIById(uiId, out GameObject ui) ? ui.GetComponent<T>() : null;
        }

        public bool IsUIVisible(ResourceDefinitionDatabase.UIResource uiResource)
        {
            return TryGetLoadedUI(uiResource, out GameObject ui) && ui.activeSelf;
        }

        public bool IsUIVisibleById(string uiId)
        {
            return TryGetLoadedUIById(uiId, out GameObject ui) && ui.activeSelf;
        }

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

        private string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }
    }
}

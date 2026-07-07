using System.Collections.Generic;
using BokeGameJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BokeGameJam.UI
{
    /// <summary>
    /// Runtime UI manager. By default, UI prefabs are loaded from Resources/Prefabs/UI/.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private const string DefaultUIResourcePath = "Prefabs/UI/";

        [Header("UI Prefab Loading")]
        [SerializeField] private string uiResourcePath = DefaultUIResourcePath;

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
        public GameObject LoadUI(string uiName)
        {
            return LoadUI(uiName, UIRoot);
        }

        /// <summary>
        /// Loads a UI prefab under the given parent if needed, then shows it.
        /// </summary>
        public GameObject LoadUI(string uiName, Transform parent)
        {
            string cacheKey = BuildResourcePath(uiName);
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

            GameObject prefab = ResourcesManager.LoadUIAtPath(cacheKey);
            if (prefab == null)
                return null;

            Transform targetParent = parent != null ? parent : UIRoot;
            GameObject uiInstance = Instantiate(prefab, targetParent, false);
            uiInstance.name = GetDisplayName(cacheKey);
            uiInstance.SetActive(true);
            loadedUIs[cacheKey] = uiInstance;

            return uiInstance;
        }

        public T LoadUI<T>(string uiName) where T : Component
        {
            GameObject ui = LoadUI(uiName);
            return ui != null ? ui.GetComponent<T>() : null;
        }

        public bool HideUI(string uiName)
        {
            if (!TryGetLoadedUI(uiName, out GameObject ui))
                return false;

            ui.SetActive(false);
            return true;
        }

        public bool CloseUI(string uiName)
        {
            string cacheKey = BuildResourcePath(uiName);
            if (string.IsNullOrEmpty(cacheKey))
                return false;

            if (!loadedUIs.TryGetValue(cacheKey, out GameObject ui))
                return false;

            loadedUIs.Remove(cacheKey);

            if (ui != null)
                Destroy(ui);

            return true;
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

        public bool TryGetLoadedUI(string uiName, out GameObject ui)
        {
            string cacheKey = BuildResourcePath(uiName);
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

        public T GetLoadedUI<T>(string uiName) where T : Component
        {
            return TryGetLoadedUI(uiName, out GameObject ui) ? ui.GetComponent<T>() : null;
        }

        public bool IsUIVisible(string uiName)
        {
            return TryGetLoadedUI(uiName, out GameObject ui) && ui.activeSelf;
        }

        private void EnsureUIRoot()
        {
            if (uiRoot != null)
                return;

            GameObject root = new GameObject("UIRoot");
            root.transform.SetParent(transform, false);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler canvasScaler = root.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            uiRoot = root.transform;
        }

        private string BuildResourcePath(string uiName)
        {
            if (string.IsNullOrWhiteSpace(uiName))
            {
                Debug.LogWarning("[UIManager] UI name is empty.");
                return null;
            }

            string normalizedName = uiName.Trim().Replace('\\', '/');
            string normalizedRoot = string.IsNullOrWhiteSpace(uiResourcePath)
                ? string.Empty
                : uiResourcePath.Trim().Replace('\\', '/').Trim('/');

            if (string.IsNullOrEmpty(normalizedRoot))
                return normalizedName.Trim('/');

            if (normalizedName.StartsWith(normalizedRoot + "/"))
                return normalizedName.Trim('/');

            return $"{normalizedRoot}/{normalizedName.Trim('/')}";
        }

        private string GetDisplayName(string resourcePath)
        {
            int lastSlashIndex = resourcePath.LastIndexOf('/');
            return lastSlashIndex >= 0 ? resourcePath[(lastSlashIndex + 1)..] : resourcePath;
        }
    }
}

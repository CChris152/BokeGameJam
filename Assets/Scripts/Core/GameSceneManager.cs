using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BokeGameJam.Core
{
    /// <summary>
    /// 全局场景管理器（单例，跨场景不销毁）。
    /// 注意：类名不要用 SceneManager，会与 Unity 内置类冲突。
    /// </summary>
    public class GameSceneManager : MonoBehaviour
    {
        public static GameSceneManager Instance { get; private set; }

        [Header("可选：异步加载时显示进度")]
        [SerializeField] private bool logLoadProgress;

        private bool isLoading;

        public bool IsLoading => isLoading;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>按场景名同步切换</summary>
        public void LoadScene(string sceneName)
        {
            if (isLoading) return;
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>按 Build Settings 索引同步切换</summary>
        public void LoadScene(int buildIndex)
        {
            if (isLoading) return;
            SceneManager.LoadScene(buildIndex);
        }

        /// <summary>按场景名异步切换（推荐，可配合加载界面）</summary>
        public void LoadSceneAsync(string sceneName)
        {
            if (isLoading) return;
            StartCoroutine(LoadSceneAsyncRoutine(sceneName));
        }

        /// <summary>重新加载当前场景</summary>
        public void ReloadCurrentScene()
        {
            LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>退出到上一个场景（简单实现：索引 -1）</summary>
        public void LoadPreviousScene()
        {
            int index = SceneManager.GetActiveScene().buildIndex;
            if (index > 0)
                LoadScene(index - 1);
        }

        private IEnumerator LoadSceneAsyncRoutine(string sceneName)
        {
            isLoading = true;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                if (logLoadProgress)
                    Debug.Log($"[GameSceneManager] 加载进度: {operation.progress * 100f:F0}%");

                yield return null;
            }

            // progress 到 0.9 后需手动激活场景
            operation.allowSceneActivation = true;

            while (!operation.isDone)
                yield return null;

            isLoading = false;
        }
    }
}
